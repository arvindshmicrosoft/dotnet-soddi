﻿using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Soddi;
using Spectre.Cli.Extensions.DependencyInjection;
using Spectre.Console.Cli;

#if DEBUG
//args = new[] { "import", @"C:\Users\phils\Downloads\aviation.stackexchange.com\", "--dropAndCreate" };
// args = new[] { "list" };
// args = new[] { "download", "sp", "-p" };
// args = new[] { "torrent", "stack", "-p" };
// args = new[] { "download", "iota" };
// args = new[] { "import", @"iota.stackexchange.com.7z", "--dropAndCreate" };
args = new[] { "list", "-h" };
#endif

{
    var container = new ServiceCollection()
        .AddSingleton<IFileSystem>(new FileSystem())
        .Scan(scan => scan.FromCallingAssembly().AddClasses());

    using var registrar = new DependencyInjectionRegistrar(container);
    var app = new CommandApp(registrar);

    app.Configure(
        config =>
        {
            config.SetApplicationName("soddi");
            config.AddCommandWithExample<ImportHandler>("import", ImportOptions.Examples);
            config.AddCommandWithExample<ListHandler>("list", ListOptions.Examples);
            config.AddCommandWithExample<DownloadHandler>("download", DownloadOptions.Examples);
            config.AddCommandWithExample<TorrentHandler>("torrent", TorrentOptions.Examples);
        });

    await app.RunAsync(args);
}
