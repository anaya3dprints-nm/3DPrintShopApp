using System.Text;
using Microsoft.AspNetCore.Identity;

namespace ThreeDPrintStore.Services;

public static class AdminPasswordHashCommand
{
    public static int Run()
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("An interactive terminal is required.");
            return 1;
        }

        var password = ReadPassword("New admin password: ");
        var confirmation = ReadPassword("Confirm admin password: ");
        if (password.Length < 12 || password.Length > 1024 || password != confirmation)
        {
            Console.Error.WriteLine("Passwords must match and be between 12 and 1024 characters.");
            return 1;
        }

        Console.WriteLine("Store this hash securely as Admin:PasswordHash:");
        Console.WriteLine(new PasswordHasher<string>().HashPassword("admin", password));
        return 0;
    }

    private static string ReadPassword(string prompt)
    {
        Console.Error.Write(prompt);
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                password.Length--;
            else if (!char.IsControl(key.KeyChar) && password.Length < 1025)
                password.Append(key.KeyChar);
        }
        Console.Error.WriteLine();
        return password.ToString();
    }
}
