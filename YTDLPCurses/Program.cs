using System.Diagnostics;
using Mindmagma.Curses;

namespace YTDLPCurses;

internal static class Program
{
    private static readonly string[] MenuItems =
    [
        "Download Video",
        "Download Video Playlist",
        "Extract Audio",
        "Extract Audio Playlist",
        "Quit"
    ];

    private static readonly string YtDlpPath = $"{Environment.GetEnvironmentVariable("HOME")}/.local/bin/yt-dlp";
    private const string FfmpegPath = "/usr/bin/ffmpeg";
    private static readonly string DenoPath = $"{Environment.GetEnvironmentVariable("HOME")}/.deno/bin/deno";
    private static readonly string OutputBase = $"{Environment.GetEnvironmentVariable("HOME")}/Videos/yt-dlp-output";

    private static void Main()
    {
        IntPtr screen = NCurses.InitScreen();
        NCurses.NoEcho();
        NCurses.CBreak();
        NCurses.Keypad(screen, true);
        NCurses.SetCursor(CursesCursorState.INVISIBLE);

        int selectedIndex = 0;
        bool running = true;

        while (running)
        {
            DrawMenu(screen, selectedIndex);

            int key = NCurses.GetChar();

            switch (key)
            {
                case CursesKey.UP:
                    selectedIndex = (selectedIndex - 1 + MenuItems.Length) % MenuItems.Length;
                    break;
                case CursesKey.DOWN:
                    selectedIndex = (selectedIndex + 1) % MenuItems.Length;
                    break;
                case 10: // Enter key
                case CursesKey.ENTER:
                    if (selectedIndex == 4) // Quit
                    {
                        running = false;
                    }
                    else
                    {
                        string? url = GetUrlInput(screen);
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            ExecuteCommand(screen, selectedIndex, url);
                        }
                    }
                    break;
                case 'q':
                case 'Q':
                    running = false;
                    break;
            }
        }

        NCurses.EndWin();
    }

    private static void DrawMenu(nint screen, int selectedIndex)
    {
        NCurses.ClearWindow(screen);
        NCurses.GetMaxYX(screen, out int maxY, out int maxX);

        const string title = "yt-dlp Download Manager";
        NCurses.MoveAddString(1, (maxX - title.Length) / 2, title);
        NCurses.MoveAddString(2, (maxX - 23) / 2, "=======================");

        const int startY = 4;
        for (int i = 0; i < MenuItems.Length; i++)
        {
            string prefix = i == selectedIndex ? " > " : "   ";
            string item = $"{prefix}{i + 1}. {MenuItems[i]}";

            if (i == selectedIndex)
            {
                NCurses.WindowAttributeOn(screen, CursesAttribute.REVERSE);
            }

            NCurses.MoveAddString(startY + i, (maxX - item.Length) / 2 - 5, item);

            if (i == selectedIndex)
            {
                NCurses.WindowAttributeOff(screen, CursesAttribute.REVERSE);
            }
        }

        const string instructions = "Use UP/DOWN arrows to navigate, ENTER to select, Q to quit";
        NCurses.MoveAddString(maxY - 2, (maxX - instructions.Length) / 2, instructions);

        NCurses.WindowRefresh(screen);
    }

    private static string? GetUrlInput(nint screen)
    {
        NCurses.ClearWindow(screen);
        NCurses.GetMaxYX(screen, out int maxY, out int maxX);

        const string prompt = "Enter URL (ESC to cancel):";
        NCurses.MoveAddString(maxY / 2 - 1, (maxX - prompt.Length) / 2, prompt);

        NCurses.SetCursor(CursesCursorState.NORMAL);
        NCurses.Echo();

        int inputY = maxY / 2 + 1;
        const int inputX = 5;
        NCurses.Move(inputY, inputX);

        var urlBuilder = new System.Text.StringBuilder();
        int cursorPos = 0;

        while (true)
        {
            NCurses.Move(inputY, inputX);
            NCurses.ClearToEndOfLine();
            NCurses.AddString(urlBuilder.ToString());
            NCurses.Move(inputY, inputX + cursorPos);
            NCurses.WindowRefresh(screen);

            int ch = NCurses.GetChar();

            switch (ch)
            {
                // ESC
                case 27:
                    NCurses.NoEcho();
                    NCurses.SetCursor(CursesCursorState.INVISIBLE);
                    return null;
                case 10:
                // Enter
                case CursesKey.ENTER:
                    NCurses.NoEcho();
                    NCurses.SetCursor(CursesCursorState.INVISIBLE);
                    return urlBuilder.ToString();
                case CursesKey.BACKSPACE:
                case 127:
                // Backspace
                case 8:
                {
                    if (cursorPos > 0)
                    {
                        urlBuilder.Remove(cursorPos - 1, 1);
                        cursorPos--;
                    }

                    break;
                }
                case CursesKey.LEFT:
                {
                    if (cursorPos > 0) cursorPos--;
                    break;
                }
                case CursesKey.RIGHT:
                {
                    if (cursorPos < urlBuilder.Length) cursorPos++;
                    break;
                }
                // Printable characters
                case >= 32 and < 127:
                    urlBuilder.Insert(cursorPos, (char)ch);
                    cursorPos++;
                    break;
            }
        }
    }

    private static void ExecuteCommand(nint screen, int optionIndex, string url)
    {
        NCurses.EndWin(); // Temporarily exit ncurses to show command output

        string arguments = GetCommandArguments(optionIndex, url);

        Console.Clear();
        Console.WriteLine($"Executing: {YtDlpPath}");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();

        var processInfo = new ProcessStartInfo
        {
            FileName = YtDlpPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        try
        {
            using var process = Process.Start(processInfo);
            process?.WaitForExit();

            Console.WriteLine();
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("Press any key to return to menu...");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Press any key to return to menu...");
            Console.ReadKey(true);
        }

        // Reinitialize ncurses
        IntPtr newScreen = NCurses.InitScreen();
        NCurses.NoEcho();
        NCurses.CBreak();
        NCurses.Keypad(newScreen, true);
        NCurses.SetCursor(CursesCursorState.INVISIBLE);
    }

    private static string GetCommandArguments(int optionIndex, string url)
    {
        string commonArgs = $"--ignore-config --abort-on-error --no-mark-watched --ffmpeg-location \"{FfmpegPath}\" --js-runtimes deno:{DenoPath}";

        return optionIndex switch
        {
            0 => // Download Video
                $"{commonArgs} --color \"always\" --no-playlist " +
                $"-o \"{OutputBase}/single_video/%(uploader)s_%(title)s/%(uploader)s_%(title)s.%(ext)s\" " +
                "--restrict-filenames --write-description --write-info-json --write-all-thumbnails " +
                "--progress --console-title --write-subs --sub-format \"best\" --embed-subs " +
                "--embed-thumbnail --embed-metadata --embed-chapters --embed-info-json --xattrs " +
                "--convert-thumbnails \"png\" --sponsorblock-mark \"all\" --sponsorblock-remove \"sponsor\" " +
                $"--recode-video \"mkv\" -t mkv -t sleep \"{url}\"",

            1 => // Download Video Playlist
                $"{commonArgs} --color \"always\" --yes-playlist " +
                $"-o \"{OutputBase}/playlist_video/%(playlist_uploader)s_%(playlist)s/%(playlist_index)s_%(title)s/%(playlist_index)s_%(title)s.%(ext)s\" " +
                "--restrict-filenames --write-playlist-metafiles --write-description --write-info-json " +
                "--write-all-thumbnails --progress --console-title --write-subs --sub-format \"best\" " +
                "--embed-subs --embed-thumbnail --embed-metadata --embed-chapters --embed-info-json --xattrs " +
                "--convert-thumbnails \"png\" --sponsorblock-mark \"all\" --sponsorblock-remove \"sponsor\" " +
                $"--recode-video \"mkv\" -t mkv -t sleep \"{url}\"",

            2 => // Extract Audio
                $"{commonArgs} --color \"auto-tty\" --no-playlist " +
                $"-o \"{OutputBase}/single_audio/%(uploader)s_%(title)s/%(uploader)s_%(title)s.%(ext)s\" " +
                "--restrict-filenames --write-description --write-info-json --progress --console-title " +
                $"--embed-metadata --xattrs --no-sponsorblock -t mp3 -t sleep \"{url}\"",

            3 => // Extract Audio Playlist
                $"{commonArgs} --color \"auto-tty\" --yes-playlist " +
                $"-o \"{OutputBase}/playlist_audio/%(playlist_uploader)s_%(playlist)s/%(playlist_index)s_%(title)s/%(playlist_index)s_%(title)s.%(ext)s\" " +
                "--restrict-filenames --write-playlist-metafiles --write-description --write-info-json " +
                $"--progress --console-title --embed-metadata --xattrs --no-sponsorblock -t mkv -t sleep \"{url}\"",

            _ => throw new ArgumentOutOfRangeException(nameof(optionIndex))
        };
    }
}
