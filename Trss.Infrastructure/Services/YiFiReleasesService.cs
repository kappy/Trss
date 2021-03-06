﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Trss.Infrastructure.Services
{
    public class YiFiReleasesService : IReleasesService 
    {
        private static readonly HttpClient Client = new HttpClient();
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings()
        {
            ContractResolver = new UnderscorePropertyNamesContractResolver()
        };

        static YiFiReleasesService()
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<Releases> GetReleases(string searchTitle, string quality, string sort, int page)
        {
            var baseAddress = new Uri("https://yts.am/api/v2/list_movies.json");
            

            var queryString = "?limit=24";
            if (page > 1)
            {
                queryString += "&page=" + page;
            }
            if (!string.IsNullOrEmpty(quality))
            {
                queryString += "&quality=" + Uri.EscapeUriString(quality);
            }
            else
            {
                queryString += "&quality=" + Uri.EscapeUriString("720p");
            }
            if (!string.IsNullOrEmpty(sort))
            {
                queryString += "&sort_by=" + Uri.EscapeUriString(sort);
            }
            else
            {
                queryString += "&sort_by=" + Uri.EscapeUriString("download_count");
            }
            queryString += "&order_by=" + Uri.EscapeUriString("desc");
            if (!string.IsNullOrEmpty(searchTitle))
            {
                queryString += "&query_term=" + Uri.EscapeUriString(searchTitle);
            }

            var response = await Client.GetAsync(baseAddress + queryString);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsStringAsync();
            var yifiReleases = JsonConvert.DeserializeObject<YiFiResponse<YiFiReleasesList>>(data, JsonSettings);
            var releases = new Releases
            {
                MovieCount = yifiReleases.Data.MovieCount,
                Movies = yifiReleases.Data.Movies?.Select(GetRelease) ?? new Release[] {}
            };
            return releases;
        }

        public async Task<Release> GetRelease(string id)
        {
            var baseAddress  = new Uri("https://yts.am/api/v2/movie_details.json");
            var querystring = $"?movie_id={id}";
            var response = await Client.GetAsync(baseAddress + querystring);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsStringAsync();
            var yifiRelease = JsonConvert.DeserializeObject<YiFiResponse<YiFiReleaseItem>>(data, JsonSettings);
            return GetRelease(yifiRelease.Data.Movie);
        }

        private Release GetRelease(YiFiRelease yiFiRelease)
        {
            //prefer the 720p if many, or if there isn't, just get the first
            var torrent = yiFiRelease.Torrents?.FirstOrDefault(x => x.Quality == "720p") ??
                          yiFiRelease.Torrents?.FirstOrDefault();
            var release = new Release
            {
                Url = torrent?.Url,
                MovieID = yiFiRelease.Id.ToString(),
                CoverImage = yiFiRelease.MediumCoverImage,
                DateUploaded = yiFiRelease.DateUploaded,
                Genre = yiFiRelease.Genres?.FirstOrDefault(),
                ImdbCode = yiFiRelease.ImdbCode,
                MovieTitleClean = yiFiRelease.TitleEnglish ?? yiFiRelease.Title,
                MovieYear = yiFiRelease.Year,
                Quality = torrent?.Quality,
                ReleaseGroup = "yts",
                Size = torrent?.Size,
                SizeByte = torrent?.SizeBytes ?? 0,
                TorrentHash = torrent?.Hash,
                TorrentPeers = torrent?.Peers ?? 0,
                TorrentSeeds = torrent?.Seeds ?? 0
            };
            return release;
        }
    }
}
