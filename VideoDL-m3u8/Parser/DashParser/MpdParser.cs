using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using VideoDL_m3u8.Extensions;

namespace VideoDL_m3u8.DashParser
{
    internal class MpdParser
    {
        public Mpd Parse(string manifest, string mpdUrl = "")
        {
            var element = GetMPD(manifest);
            var mediaPresentationDuration = element
                .GetAttribute("mediaPresentationDuration").ParseTimeSpan();
            var childNodes = element.ChildNodes.AsEnumerable();
            var baseUrl = childNodes
                .FirstOrDefault(it => it.Name == "BaseURL")?.InnerText ?? "";
            var periods = childNodes
                .Where(it => it.Name == "Period")
                .Select(it => GetPeriod((XmlElement)it))
                .ToList();
            var mpd = new Mpd
            {
                Periods = periods,
                BaseUrl = baseUrl,
                MediaPresentationDuration = mediaPresentationDuration
            };
            mpd = GenerateSegmentTemplate(mpd);
            mpd = GenerateSegmentUrl(mpd, mpdUrl);
            return mpd;
        }

        protected XmlElement GetMPD(string manifest)
        {
            var doc = new XmlDocument();
            doc.LoadXml(manifest);
            var mpd = doc.ChildNodes.AsEnumerable()
                .Where(it =>
                    it.Name == "MPD" &&
                    it.NodeType == XmlNodeType.Element)
                .FirstOrDefault();
            if (mpd == null)
                throw new Exception("Not found MPD tag.");
            return (XmlElement)mpd;
        }

        protected Period GetPeriod(XmlElement element)
        {
            var duration = element
                .GetAttribute("duration").ParseTimeSpan();
            var childNodes = element.ChildNodes.AsEnumerable();
            var adaptationSets = childNodes
                .Where(it => it.Name == "AdaptationSet")
                .Select(it => GetAdaptationSet((XmlElement)it))
                .ToList();
            return new Period
            {
                AdaptationSets = adaptationSets,
                Duration = duration
            };
        }

        protected AdaptationSet GetAdaptationSet(XmlElement element)
        {
            var lang = element.GetAttribute("lang");
            var segmentAlignment = element.GetAttribute("segmentAlignment");
            var childNodes = element.ChildNodes.AsEnumerable();
            var representations = childNodes
                .Where(it => it.Name == "Representation")
                .Select(it => GetRepresentation((XmlElement)it))
                .ToList();
            var baseUrl = childNodes
                .FirstOrDefault(it => it.Name == "BaseURL")?.InnerText ?? "";
            return new AdaptationSet
            {
                Representations = representations,
                BaseUrl = baseUrl,
                Lang = lang,
                SegmentAlignment = segmentAlignment != "" ?
                    segmentAlignment.ParseBool() ?? false : false
            };
        }

        protected Representation GetRepresentation(XmlElement element)
        {
            var id = element.GetAttribute("id");
            var mimeType = element.GetAttribute("mimeType");
            var codecs = element.GetAttribute("codecs");
            var bandwidth = element.GetAttribute("bandwidth");
            var width = element.GetAttribute("width");
            var height = element.GetAttribute("height");
            var audioSamplingRate = element.GetAttribute("audioSamplingRate");
            var frameRate = element.GetAttribute("frameRate");
            var startWithSAP = element.GetAttribute("startWithSAP");
            var childNodes = element.ChildNodes.AsEnumerable();
            var segmentList = childNodes
                .FirstOrDefault(it => it.Name == "SegmentList");
            var segmentTemplate = childNodes
                .FirstOrDefault(it => it.Name == "SegmentTemplate");
            var baseUrl = childNodes
                .FirstOrDefault(it => it.Name == "BaseURL")?.InnerText ?? "";
            return new Representation
            {
                SegmentList = segmentList != null ?
                    GetSegmentList((XmlElement)segmentList) : new(),
                SegmentTemplate = segmentTemplate != null ?
                    GetSegmentTemplate((XmlElement)segmentTemplate) : null,
                BaseUrl = baseUrl,
                Id = id,
                MimeType = mimeType,
                Codecs = codecs,
                FrameRate = frameRate,
                Bandwidth = bandwidth != "" ? int.Parse(bandwidth) : null,
                Width = width != "" ? int.Parse(width) : null,
                Height = height != "" ? int.Parse(height) : null,
                AudioSamplingRate = audioSamplingRate != "" ? int.Parse(audioSamplingRate) : null,
                StartWithSAP = startWithSAP != "" ? int.Parse(startWithSAP) : null
            };
        }

        protected SegmentList GetSegmentList(XmlElement element)
        {
            var duration = element.GetAttribute("duration");
            var timescale = element.GetAttribute("timescale");
            var childNodes = element.ChildNodes.AsEnumerable();
            var initialization = childNodes
                .FirstOrDefault(it => it.Name == "Initialization");
            var segmentUrls = childNodes
                .Where(it => it.Name == "SegmentURL")
                .Select(it => GetSegmentUrl((XmlElement)it))
                .ToList();
            var segmentList = new SegmentList
            {
                Initialization = initialization != null ?
                   GetInitialization((XmlElement)initialization) : null,
                SegmentUrls = segmentUrls,
                Timescale = timescale != "" ? int.Parse(timescale) : null,
                Duration = duration != "" ? int.Parse(duration) : null
            };
            foreach(var item in segmentUrls)
            {
                item.Timescale = segmentList.Timescale ?? 1;
                item.Duration = segmentList.Duration ?? 0;
            }
            return segmentList;
        }

        protected Initialization GetInitialization(XmlElement element)
        {
            var sourceURL = element.GetAttribute("sourceURL");
            var range = element.GetAttribute("range");
            return new Initialization
            {
                SourceURL = sourceURL,
                Range = range != "" ?
                    new Range
                    {
                        From = long.Parse(range.Split('-')[0]),
                        To = long.Parse(range.Split('-')[1])
                    } : null
            };
        }

        protected SegmentUrl GetSegmentUrl(XmlElement element)
        {
            var media = element.GetAttribute("media");
            var index = element.GetAttribute("index");
            var mediaRange = element.GetAttribute("mediaRange");
            var indexRange = element.GetAttribute("indexRange");
            return new SegmentUrl
            {
                Media = media,
                Index = index,
                MediaRange = mediaRange != "" ?
                    new Range
                    {
                        From = long.Parse(mediaRange.Split('-')[0]),
                        To = long.Parse(mediaRange.Split('-')[1])
                    } : null,
                IndexRange = indexRange != "" ?
                    new Range
                    {
                        From = long.Parse(indexRange.Split('-')[0]),
                        To = long.Parse(indexRange.Split('-')[1])
                    } : null
            };
        }

        protected SegmentTemplate GetSegmentTemplate(XmlElement element)
        {
            var initialization = element.GetAttribute("initialization");
            var media = element.GetAttribute("media");
            var startNumber = element.GetAttribute("startNumber");
            var duration = element.GetAttribute("duration");
            var timescale = element.GetAttribute("timescale");
            var childNodes = element.ChildNodes.AsEnumerable();
            var segmentTimeline = childNodes
                .FirstOrDefault(it => it.Name == "SegmentTimeline");
            return new SegmentTemplate
            {
                SegmentTimelines = segmentTimeline != null ?
                    GetSegmentTimelines((XmlElement)segmentTimeline) : new(),
                Initialization = initialization,
                Media = media,
                StartNumber = startNumber != "" ? int.Parse(startNumber) : null,
                Timescale = timescale != "" ? int.Parse(timescale) : null,
                Duration = duration != "" ? int.Parse(duration) : null
            };
        }

        protected List<SegmentTimeline> GetSegmentTimelines(XmlElement element)
        {
            SegmentTimeline GetSegmentTimeline(XmlElement element)
            {
                var t = element.GetAttribute("t");
                var d = element.GetAttribute("d");
                var r = element.GetAttribute("r");
                return new SegmentTimeline
                {
                    T = t != "" ? int.Parse(t) : null,
                    D = d != "" ? int.Parse(d) : null,
                    R = r != "" ? int.Parse(r) : null
                };
            }
            var childNodes = element.ChildNodes.AsEnumerable();
            return childNodes
                .Where(it => it.Name == "S")
                .Select(it => GetSegmentTimeline((XmlElement)it))
                .ToList();
        }

        protected Mpd GenerateSegmentUrl(Mpd mpd, string mpdUrl)
        {
            var baseUrl = mpdUrl;
            if (!string.IsNullOrEmpty(mpd.BaseUrl))
                baseUrl = string.IsNullOrEmpty(baseUrl) ?
                    mpd.BaseUrl : baseUrl.CombineUri(mpd.BaseUrl);

            foreach (var period in mpd.Periods)
            {
                foreach (var adaptationSet in period.AdaptationSets)
                {
                    var adaBaseUrl = baseUrl;
                    if (!string.IsNullOrEmpty(adaptationSet.BaseUrl))
                        adaBaseUrl = string.IsNullOrEmpty(adaBaseUrl) ?
                            adaptationSet.BaseUrl : adaBaseUrl.CombineUri(adaptationSet.BaseUrl);

                    foreach (var representation in adaptationSet.Representations)
                    {
                        var repBaseUrl = adaBaseUrl;
                        if (!string.IsNullOrEmpty(representation.BaseUrl))
                            repBaseUrl = string.IsNullOrEmpty(repBaseUrl) ?
                                representation.BaseUrl : repBaseUrl.CombineUri(representation.BaseUrl);
                        
                        var segmentList = representation.SegmentList;
                        if (segmentList.Initialization != null)
                        {
                            var initialization = segmentList.Initialization;
                            var initUrl = repBaseUrl;
                            if (!string.IsNullOrEmpty(initialization.SourceURL))
                                initUrl = string.IsNullOrEmpty(initUrl) ?
                                    initialization.SourceURL : initUrl.CombineUri(initialization.SourceURL);
                            initialization.SourceURL = initUrl;
                        }
                        foreach (var segmentUrl in segmentList.SegmentUrls)
                        {
                            var mediaUrl = repBaseUrl;
                            if (!string.IsNullOrEmpty(segmentUrl.Media))
                                mediaUrl = string.IsNullOrEmpty(mediaUrl) ?
                                    segmentUrl.Media : mediaUrl.CombineUri(segmentUrl.Media);
                            segmentUrl.Media = mediaUrl;

                            var indexUrl = repBaseUrl;
                            if (!string.IsNullOrEmpty(segmentUrl.Index))
                                indexUrl = string.IsNullOrEmpty(indexUrl) ?
                                    segmentUrl.Index : indexUrl.CombineUri(segmentUrl.Index);
                            segmentUrl.Index = indexUrl;
                        }
                    }
                }
            }
            return mpd;
        }

        protected Mpd GenerateSegmentTemplate(Mpd mpd)
        {
            foreach (var period in mpd.Periods)
            {
                var periodDuration = 0.0d;
                if (period.Duration.HasValue)
                    periodDuration = period.Duration.Value.TotalSeconds;

                foreach (var adaptationSet in period.AdaptationSets)
                {
                    foreach (var representation in adaptationSet.Representations)
                    {
                        var id = representation.Id;

                        var segmentList = representation.SegmentList;
                        var segmentTemplate = representation.SegmentTemplate;
                        if (segmentTemplate != null)
                        {
                            if (!string.IsNullOrEmpty(segmentTemplate.Initialization))
                            {
                                segmentList.Initialization = new Initialization
                                {
                                    SourceURL = segmentTemplate.Initialization
                                        .Replace("$RepresentationID$", id)
                                };
                            }

                            var totalNumber = 0;
                            var startNumber = segmentTemplate.StartNumber ?? 0;

                            var segmentTimelines = segmentTemplate.SegmentTimelines;
                            if (segmentTimelines.Count > 0)
                            {
                                var time = 0;
                                foreach (var segmentTimeline in segmentTimelines)
                                {
                                    time = segmentTimeline.T ?? time;
                                    var d = segmentTimeline.D ?? 0;
                                    var r = segmentTimeline.R ?? 0;
                                    for (var i = 0; i < r + 1; i++)
                                    {
                                        var segmentUrl = new SegmentUrl
                                        {
                                            Media = segmentTemplate.Media
                                                .Replace("$RepresentationID$", id)
                                                .Replace("$Time$", $"{time}"),
                                            Timescale = segmentTemplate.Timescale ?? 1,
                                            Duration = d
                                        };
                                        segmentList.SegmentUrls.Add(segmentUrl);
                                        time += d;
                                    }
                                }
                            }
                            else
                            {
                                if (segmentTemplate.Duration != null)
                                {
                                    var timescale = segmentTemplate.Timescale ?? 1;
                                    var duration = (double)segmentTemplate.Duration.Value / timescale;
                                    totalNumber = (int)Math.Ceiling(periodDuration / duration);
                                }
                                for (var i = startNumber; i < startNumber + totalNumber; i++)
                                {
                                    var segmentUrl = new SegmentUrl
                                    {
                                        Media = segmentTemplate.Media
                                            .Replace("$RepresentationID$", id)
                                            .Replace("$Number$", $"{i}"),
                                        Timescale = segmentTemplate.Timescale ?? 1,
                                        Duration = segmentTemplate.Duration ?? 0
                                    };
                                    segmentList.SegmentUrls.Add(segmentUrl);
                                }
                            }
                        }
                    }
                }
            }
            return mpd;
        }
    }
}
