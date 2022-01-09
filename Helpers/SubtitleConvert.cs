﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SubtitlesParser.Classes;
using SubtitlesParser.Classes.Parsers;
using SubtitlesParser.Classes.Writers;

namespace subbuzz.Helpers
{
    class SubtitleConvert
    {
        public static Stream ToSupportedFormat(Stream inStream, Encoding encoding, bool convertToUtf8, float fps, ref string format)
        {
            Stream ins = new MemoryStream();
            inStream.CopyTo(ins);

            ins.Seek(0, SeekOrigin.Begin);
            UtfUnknown.DetectionResult csDetect = UtfUnknown.CharsetDetector.DetectFromStream(ins);
            if (csDetect.Detected != null && csDetect.Detected.Confidence > 0.8)
                encoding = csDetect.Detected.Encoding ?? encoding;

            Stream outs = new MemoryStream();

            Dictionary<SubtitlesFormat, ISubtitlesParser> parsers = new Dictionary<SubtitlesFormat, ISubtitlesParser>
            {
                {SubtitlesFormat.SubRipFormat, new SrtParser()},
                {SubtitlesFormat.MicroDvdFormat, new MicroDvdParser(fps)},
                {SubtitlesFormat.SubViewerFormat, new SubViewerParser()},
                {SubtitlesFormat.SubStationAlphaFormat, new SsaParser()},
                {SubtitlesFormat.TtmlFormat, new TtmlParser()},
                {SubtitlesFormat.WebVttFormat, new VttParser()},
                {SubtitlesFormat.YoutubeXmlFormat, new YtXmlFormatParser()}
            };

            try
            {
                foreach (var parser in parsers)
                {
                    List<SubtitleItem> items;

                    try
                    {
                        items = parser.Value.ParseStream(ins, encoding);
                    }
                    catch
                    {
                        continue;
                    }

                    if (parser.Key == SubtitlesFormat.SubRipFormat ||
                        parser.Key == SubtitlesFormat.SubStationAlphaFormat ||
                        parser.Key == SubtitlesFormat.WebVttFormat)
                    {
                        // Do not convert formats supported by emby/jellyfin, just re-encode to UTF8 if needed
                        ins.Seek(0, SeekOrigin.Begin);
                        var sr = new StreamReader(ins, encoding, true);
                        var writer = new StreamWriter(outs, convertToUtf8 ? Encoding.UTF8 : encoding);
                        writer.Write(sr.ReadToEnd());
                        writer.Flush();
                        format = parser.Key.Extension.Split('.').LastOrDefault().ToLower();
                    }
                    else
                    {
                        // convert to srt
                        var writer = new SrtWriter();
                        writer.WriteStream(outs, convertToUtf8 ? Encoding.UTF8 : encoding, items, false);
                        format = "srt";
                    }

                    outs.Seek(0, SeekOrigin.Begin);
                    return outs;
                }
            }
            catch
            {
            }

            return outs;
        }
    }
}
