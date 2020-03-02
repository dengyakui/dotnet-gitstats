using System;
using LibGit2Sharp;
using CsvHelper;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace DotNetGitLineStat
{
    class Program
    {
        static int Main(string[] args)
        {
            ConfigOptions options = GetOptions(args);
            Console.WriteLine(options.ToString());

            var workdir = options.WorkDir;
            var csvPath = options.Output;

            Console.WriteLine(workdir);
            Console.WriteLine(csvPath);

            var list = GetCommitLogs(workdir);
            switch (Path.GetExtension(options.Output)?.TrimStart('.') ?? "csv")
            {
                case "json":
                    {
                        GenerateJson(csvPath, list);
                        break;
                    }

                case "csv":
                    {
                        GenerateCsv(csvPath, list);
                        break;
                    }
            }
            Console.WriteLine("done");
            return 0;

        }

        private static ConfigOptions GetOptions(string[] args)
        {
            var switchMappings = new Dictionary<string, string> {
                {"-d", "workdir"},
                {"-o", "output" },
            };
            var configRoot = new ConfigurationBuilder()
                .AddCommandLine(args, switchMappings)
                .Build();

            var options = new ConfigOptions();
            configRoot.Bind(options);
            return options;
        }

        private static List<GitCommitLogDto> GetCommitLogs(string workdir)
        {
            var list = new List<GitCommitLogDto>();
            using (var repo = new Repository(workdir))
            {
                Console.WriteLine("all commit count:" + repo.Commits.Count());
                foreach (Commit commit in repo.Commits)
                {
                    var commitDto = new GitCommitLogDto
                    {
                        CommitHash = commit.Sha,
                        AuthorName = commit.Author.Name,
                        AuthorEmail = commit.Author.Email,
                        MessageShort = commit.MessageShort,
                        AuthorDate = commit.Author.When.DateTime,
                    };

                    var patch = GetPatchInfo(repo, commit);
                    if (patch != null)
                    {
                        commitDto.LinesAdded = patch.LinesAdded;
                        commitDto.LinesDeleted = patch.LinesDeleted;
                    };
                    Console.WriteLine(commitDto.ToString());
                    list.Add(commitDto);
                }
            }

            return list;
        }

        private static void GenerateCsv(string csvPath, List<GitCommitLogDto> list)
        {
            using (var writer = new StreamWriter(csvPath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(list);
                Console.WriteLine("csv finish");
            }
        }

        private static void GenerateJson(string csvPath, List<GitCommitLogDto> list)
        {
            File.WriteAllText(csvPath, JsonConvert.SerializeObject(list));
            Console.WriteLine("json finish");
        }

        private static Patch GetPatchInfo(Repository repo, Commit commit)
        {
            var parentCommitCount = commit.Parents?.Count() ?? 0;
            var compareOption = new LibGit2Sharp.CompareOptions()
            {
                Algorithm = DiffAlgorithm.Minimal,
                Similarity = new SimilarityOptions()
                {
                    RenameDetectionMode = RenameDetectionMode.Renames
                }
            };
            if (parentCommitCount <= 0)
            {
                return repo.Diff.Compare<Patch>(null, commit.Tree, compareOption);
            }
            else if (commit.Parents.Count() == 1)
            {
                return repo.Diff.Compare<Patch>(commit.Parents.First().Tree, commit.Tree, compareOption);
            }
            return null;

        }
    }

    public class ConfigOptions
    {
        /// <summary>
        /// Git仓库目录
        /// </summary>
        public string WorkDir { get; set; }


        /// <summary>
        /// 输出文件(path/to/file.json/xml)
        /// </summary>
        public string Output { get; set; }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class GitCommitLogDto
    {
        public string CommitHash { get; set; }
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public DateTime AuthorDate { get; set; }
        public int? LinesAdded { get; set; }
        public int? LinesDeleted { get; set; }
        public string MessageShort { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
