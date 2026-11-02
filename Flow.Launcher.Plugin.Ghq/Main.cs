using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Ghq
{
    public class Main : IPlugin
    {
        private PluginInitContext _context;

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        private string GetDisplayTitle(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            var normalized = path.Replace('\\', '/').TrimEnd('/');

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                                        .Replace('\\', '/')
                                        .TrimEnd('/');

            var msysHome = $"C:/msys64/home/{Environment.UserName}".TrimEnd('/');

            if (normalized.Equals(userProfile, StringComparison.OrdinalIgnoreCase))
                return "~";

            if (normalized.Equals(msysHome, StringComparison.OrdinalIgnoreCase))
                return "~msys";

            return Path.GetFileName(path.TrimEnd('\\', '/'));
        }

        private string FormatPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            var normalized = path.Replace('\\', '/');

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                                        .Replace('\\', '/');

            if (normalized.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                return "~" + normalized[userProfile.Length..];
            }

            var msysHome = $"C:/msys64/home/{Environment.UserName}";
            if (normalized.StartsWith(msysHome, StringComparison.OrdinalIgnoreCase))
            {
                return "~msys" + normalized[msysHome.Length..];
            }

            return path;
        }

        public List<Result> Query(Query query)
        {
            var search = query.Search?.Trim() ?? "";
            var paths = RunGhq(search, out var error);

            if (paths.Count == 0)
            {
                if (!string.IsNullOrEmpty(error))
                {
                    return new List<Result>
                    {
                        new Result
                        {
                            Title = "ghq error",
                            SubTitle = error,
                            IcoPath = "assets/folder.png",
                        }
                    };
                }

                return new List<Result>
                {
                    new Result
                    {
                        Title = string.IsNullOrEmpty(search) ? "No repositories in ghq" : "No matches",
                        SubTitle = string.IsNullOrEmpty(search) ? "Use 'ghq get' to clone repositories" : $"Search: {search}",
                        IcoPath = "assets/folder.png",
                    }
                };
            }

            var lastKeyword = string.IsNullOrWhiteSpace(search)
                ? ""
                : search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";

            return paths.Select(path =>
            {
                var title = GetDisplayTitle(path);
                var subtitle = FormatPath(path);

                var titleMatch = string.IsNullOrWhiteSpace(lastKeyword)
                    ? null
                    : _context.API.FuzzySearch(lastKeyword, title);

                return new Result
                {
                    Title = title,
                    SubTitle = subtitle,
                    IcoPath = "assets/folder.png",
                    Score = titleMatch?.Score ?? 0,
                    TitleHighlightData = titleMatch?.MatchData,
                    Action = _ =>
                    {
                        _context.API.OpenDirectory(path);
                        return true;
                    }
                };
            }).ToList();
        }

        private List<string> RunGhq(string search, out string error)
        {
            error = null;
            try
            {
                var args = string.IsNullOrWhiteSpace(search)
                    ? "list -p"
                    : $"list -p \"{search}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "ghq",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    error = "Failed to start ghq process";
                    return new List<string>();
                }

                var output = process.StandardOutput.ReadToEnd();
                var err = process.StandardError.ReadToEnd();
                process.WaitForExit(3000);

                if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(err))
                {
                    error = string.IsNullOrWhiteSpace(err) ? "ghq returned an error" : err.Trim();
                    return new List<string>();
                }

                return output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Take(50)
                    .ToList();
            }
            catch (Exception ex)
            {
                error = ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    ? "ghq command not found. Make sure ghq is installed and in your PATH."
                    : ex.Message;
                return new List<string>();
            }
        }
    }
}
