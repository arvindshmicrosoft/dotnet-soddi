﻿using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using JetBrains.Annotations;
using MediatR;
using Soddi.Services;
using Spectre.Console;

namespace Soddi
{
    [Verb("download", HelpText = "Download the most recent data dump for a Stack Overflow site from archive.org"),
     UsedImplicitly]
    public class DownloadOptions : IRequest<int>
    {
        public DownloadOptions(string archive, string output, bool pick)
        {
            Archive = archive;
            Output = output;
            Pick = pick;
        }

        [Value(0, HelpText = "Archive to download", Required = true, MetaName = "Archive")]
        public string Archive { get; }

        [Option('o', "output", HelpText = "Output folder")]
        public string Output { get; }

        [Option('p', "pick", HelpText = "Pick from a list of archives to download", Default = false)]
        public bool Pick { get; }

        [Usage(ApplicationAlias = "soddi"), UsedImplicitly]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Download archive for aviation.stackexchange.com",
                    new DownloadOptions("aviation", "", false));
                yield return new Example("Download archive for math.stackexchange.com to a particular folder",
                    new DownloadOptions("math", "c:\\stack-data", false));
                yield return new Example("Pick from archives containing \"stack\" and download",
                    new DownloadOptions("stack", "", true));
            }
        }
    }

    public class DownloadHandler : IRequestHandler<DownloadOptions, int>
    {
        private readonly IFileSystem _fileSystem;

        public DownloadHandler(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public async Task<int> Handle(DownloadOptions request, CancellationToken cancellationToken)
        {
            var outputPath = request.Output;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = _fileSystem.Directory.GetCurrentDirectory();
            }

            if (!_fileSystem.Directory.Exists(outputPath))
            {
                throw new SoddiException($"Output path {outputPath} not found");
            }

            var availableArchiveParser = new AvailableArchiveParser();
            var archiveUrl = await availableArchiveParser.FindOrPickArchive(request.Archive, request.Pick, cancellationToken);

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new SpinnerColumn(), new FixedTaskDescriptionColumn(Math.Clamp(AnsiConsole.Width, 40, 65)),
                    new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn(),
                }).StartAsync(async ctx =>
                {
                    List<(ProgressTask Task, Archive.UriWithSize UriWithSize)> tasks = archiveUrl.Uris
                        .Select(uriWithSize => (ctx.AddTask(uriWithSize.Description()), uriWithSize))
                        .ToList();

                    while (!ctx.IsFinished)
                    {
                        foreach (var (task, uriWithSize) in tasks)
                        {
                            var progress = new Progress<(int downloadedInKb, int totalSizeInKb)>(i =>
                                {
                                    var progressTask = task;
                                    var (downloadedInKb, totalSizeInKb) = i;

                                    progressTask.Increment(downloadedInKb);
                                    progressTask.MaxValue(totalSizeInKb);

                                    var description =
                                        $"{uriWithSize.Description(false)} - {progressTask.Value.KiloBytesToString()}/{progressTask.MaxValue.KiloBytesToString()}";
                                    progressTask.Description(description);
                                }
                            );

                            var downloader = new ArchiveDownloader(outputPath, progress);
                            await downloader.Go(uriWithSize.Uri, cancellationToken);
                        }
                    }
                });

            return await Task.FromResult(0);
        }
    }
}
