using System;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;

namespace Revisory_Control.Grabber
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);


        private const string API_URL = "https://localhost:5001/api/";
        private const int MINUTE = 60_000;
        private static LoginUser userToLogin { get; set; }
        private static User user { get; set; }
        private static ResponseToken token { get; set; }

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode;

            bool isLoggedIn = false;

            using var client = new HttpClient();
            client.DefaultRequestHeaders
                  .Accept
                  .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            do
            {
                LoginUser userToLogin = GetUserData();

                HttpResponseMessage loginResult = await LoginUser(userToLogin, client);

                if (loginResult.IsSuccessStatusCode)
                {
                    isLoggedIn = true;
                    token = loginResult.Content.ReadAsAsync<ResponseToken>().Result;
                }
                else
                {
                    Console.WriteLine("\nПомилка: " + loginResult.Content.ReadAsStringAsync().Result + ". Введіть дані знову.\n");
                }

            } while (!isLoggedIn);

            int userId = token.UserId;

            HttpResponseMessage userResult = await client.GetAsync(API_URL + "users/" + userId);

            user = userResult.Content.ReadAsAsync<User>().Result;

            Console.WriteLine($"\nКористувач: {user.Firstname} {user.Lastname}. \nВікно згорнеться через 3 секунди...");

            GC.Collect();

            Thread.Sleep(3000);

            ShowWindow(GetConsoleWindow(), SW_HIDE);

            Timer timer = new Timer(isUserWorking, null, 0, MINUTE);

            Console.ReadLine();
        }

        private async static void isUserWorking(Object state)
        {
            using var client = new HttpClient();

            try
            {
                HttpResponseMessage addMinuteResult = await client.PutAsync(API_URL + $"users/{user.UserId}/working/{isAllowedApp()}", null);
                addMinuteResult.EnsureSuccessStatusCode();
            }
            catch (Exception)
            {
                HttpResponseMessage addMinuteResult = await client.PutAsync(API_URL + $"users/{user.UserId}/working/{false}", null);
            }
        }

        private static bool isAllowedApp()
        {
            return (GetActiveWindowTitle().Contains("Visual") ||
                    GetActiveWindowTitle().Contains("Studio") ||
                    GetActiveWindowTitle().Contains("Git")) && GetActiveWindowTitle() != null;
        }

        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        public static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        // remove one character from the list of password characters
                        password = password.Substring(0, password.Length - 1);
                        // get the location of the cursor
                        int pos = Console.CursorLeft;
                        // move the cursor to the left by one character
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        // replace it with space
                        Console.Write(" ");
                        // move the cursor to the left by one character again
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }
            // add a new line because user pressed enter at the end of their password
            Console.WriteLine();
            return password;
        }

        private static async Task<HttpResponseMessage> LoginUser(LoginUser userToLogin, HttpClient client)
        {
            var json = JsonConvert.SerializeObject(userToLogin);
            var userData = new StringContent(json, Encoding.UTF8, "application/json");

            var loginResult = await client.PostAsync(API_URL + "account/login", userData);
            return loginResult;
        }

        private static LoginUser GetUserData()
        {
            Console.Write("E-mail: ");
            string email = Console.ReadLine();
            Console.Write("\nПароль: ");
            string password = ReadPassword();

            LoginUser userToLogin = new LoginUser
            {
                Email = email,
                Password = password
            };
            return userToLogin;
        }
    }
}
