using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace JiggleForge.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string applicationDirectory = Path.Combine(root, "App");
        string executable = Path.Combine(applicationDirectory, "JiggleForge.exe");
        if (!File.Exists(executable))
        {
            MessageBox.Show(
                "找不到 App\\JiggleForge.exe。请重新解压完整的 JiggleForge 发布包。\r\n\r\n" +
                "App\\JiggleForge.exe is missing. Extract the complete JiggleForge package again.",
                "JiggleForge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(executable, JoinArguments(args))
            {
                WorkingDirectory = applicationDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "无法启动 JiggleForge。\r\n\r\n" + exception.Message +
                "\r\n\r\nJiggleForge could not be started.",
                "JiggleForge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string JoinArguments(string[] args) =>
        string.Join(" ", args.Select(QuoteArgument));

    private static string QuoteArgument(string value)
    {
        if (value.Length > 0 && value.All(character =>
                !char.IsWhiteSpace(character) && character != '"'))
        {
            return value;
        }

        StringBuilder result = new("\"");
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                result.Append('\\', backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }

            result.Append('\\', backslashes);
            backslashes = 0;
            result.Append(character);
        }

        result.Append('\\', backslashes * 2);
        result.Append('"');
        return result.ToString();
    }
}
