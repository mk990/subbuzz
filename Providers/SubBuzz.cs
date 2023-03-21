﻿using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using subbuzz.Extensions;
using subbuzz.Helpers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

#if EMBY
using MediaBrowser.Common.Net;
using ILogger = MediaBrowser.Model.Logging.ILogger;
#else
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger<subbuzz.Providers.SubBuzz>;
#endif

#if JELLYFIN
using System.Net.Http;
#endif

namespace subbuzz.Providers
{
    public class SubBuzz : ISubtitleProvider, IHasOrder
    {
        public string Name => $"{Plugin.NAME}";

        public IEnumerable<VideoContentType> SupportedMediaTypes =>
            new List<VideoContentType> { VideoContentType.Episode, VideoContentType.Movie };

        public int Order => 0;

        private readonly ILogger _logger;
        private readonly Dictionary<string, ISubBuzzProvider> Providers;

        public SubBuzz(
            ILogger logger,
            IFileSystem fileSystem,
            ILocalizationManager localizationManager,
            ILibraryManager libraryManager,
#if JELLYFIN
            IHttpClientFactory http
#else
            IHttpClient http
#endif
            )
        {
            _logger = logger;
            Plugin.Instance.InitCache();
            Providers = new Dictionary<string, ISubBuzzProvider>
            {
                { SubsSabBz.NAME,           new SubsSabBz(logger, fileSystem, localizationManager, libraryManager, http) },
                { SubsUnacsNet.NAME,        new SubsUnacsNet(logger, fileSystem, localizationManager, libraryManager, http) },
                { YavkaNet.NAME,            new YavkaNet(logger, fileSystem, localizationManager, libraryManager, http) },
                { OpenSubtitlesCom.NAME,    new OpenSubtitlesCom(logger, fileSystem, localizationManager, libraryManager, http) },
                { PodnapisiNet.NAME,        new PodnapisiNet(logger, fileSystem, localizationManager, libraryManager, http) },
                { Subf2m.NAME,              new Subf2m(logger, fileSystem, localizationManager, libraryManager, http) },
                { Subscene.NAME,            new Subscene(logger, fileSystem, localizationManager, libraryManager, http) },
                { YifySubtitles.NAME,       new YifySubtitles(logger, fileSystem, localizationManager, libraryManager, http) },
            };
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            foreach (var p in Providers)
            {
                if (id.StartsWith(p.Key))
                {
                    return await p.Value.GetSubtitles(id.Substring(p.Key.Length), cancellationToken);
                }
            }

            return new SubtitleResponse();
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request,
            CancellationToken cancellationToken)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var tasks = new Dictionary<string, Task<IEnumerable<RemoteSubtitleInfo>>>();

            _logger.LogInformation($"{Name}: Start subtitle search for {request.Name}.");

            foreach (var p in Providers)
            {
                if (!p.Value.SupportedMediaTypes.Contains(request.ContentType)) continue;
                tasks.Add(p.Key, p.Value.Search(request, cancellationToken));
            }

            var res = new List<SubtitleInfo>();

            foreach (var task in tasks)
            {
#if JELLYFIN
                // Jellyfin search request times out after 30 seconds, so ignore searches not completed in time.
                var elapsedTime = watch.ElapsedMilliseconds;
                if (!task.Value.Wait((int)(elapsedTime >= 29000 ? 1 : 29000 - elapsedTime)))
                {
                    _logger.LogInformation($"{Name}: The response from {task.Key} is ignored because it did not complete in time.");
                    continue;
                }
#endif

                List<SubtitleInfo> subs = (List<SubtitleInfo>)await task.Value;

                foreach (var s in subs)
                {
                    s.Id = task.Key + s.Id;
                    s.SubBuzzProviderName = task.Key;
                    s.ProviderName = Name;
#if NO_HTML
                    var parser = new AngleSharp.Html.Parser.HtmlParser();
                    var nodeList = parser.ParseFragment(s.Name, null);
                    s.Name = string.Concat(nodeList.Select(x => x.TextContent));

                    var regex = new System.Text.RegularExpressions.Regex(@"<br.*?>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    s.Comment = regex.Replace(s.Comment, " &#9734; ");

                    nodeList = parser.ParseFragment(s.Comment, null);
                    s.Comment = $"[{task.Key}] " + string.Concat(nodeList.Select(x => x.TextContent));
#else
                    s.Comment = $"<b>[{task.Key}]</b> " + s.Comment;
#endif
                }

                Utils.MergeSubtitleInfo(res, subs);
            }

            if (request.IsPerfectMatch)
            {
                res.RemoveAll(i => (i.IsHashMatch ?? false) == false);
            }

            res.Sort((x, y) => y.Score.CompareTo(x.Score));

            watch.Stop();
            _logger.LogInformation($"{Name}: Search duration: {watch.ElapsedMilliseconds/1000.0} sec. Subtitles found: {res.Count}");

            return res;
        }
    }
}
