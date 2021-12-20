﻿using System;
using System.Linq;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Statistics.Domain;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;
using Tweetinvi;
using Tweetinvi.Models;

namespace BirdsiteLive.Twitter
{
    public interface ITwitterUserService
    {
        TwitterUser GetUser(string username);
    }

    public class TwitterUserService : ITwitterUserService
    {
        private readonly ITwitterAuthenticationInitializer _twitterAuthenticationInitializer;
        private readonly ITwitterStatisticsHandler _statisticsHandler;
        private readonly ILogger<TwitterUserService> _logger;

        #region Ctor
        public TwitterUserService(ITwitterAuthenticationInitializer twitterAuthenticationInitializer, ITwitterStatisticsHandler statisticsHandler, ILogger<TwitterUserService> logger)
        {
            _twitterAuthenticationInitializer = twitterAuthenticationInitializer;
            _statisticsHandler = statisticsHandler;
            _logger = logger;
        }
        #endregion

        public TwitterUser GetUser(string username)
        {
            _twitterAuthenticationInitializer.EnsureAuthenticationIsInitialized();
            ExceptionHandler.SwallowWebExceptions = false;

            IUser user;
            try
            {
                user = User.GetUserFromScreenName(username);
                _statisticsHandler.CalledUserApi();
                if (user == null)
                {
                    _logger.LogWarning("User {username} not found", username);
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving user {Username}", username);

                // TODO keep track of error, see where to remove user if too much errors

                return null;
            }

            // Expand URLs
            var description = user.Description;
            foreach (var descriptionUrl in user.Entities?.Description?.Urls?.OrderByDescending(x => x.URL.Length))
                description = description.Replace(descriptionUrl.URL, descriptionUrl.ExpandedURL);

            return new TwitterUser
            {
                Id = user.Id,
                Acct = username,
                Name = user.Name,
                Description = description,
                Url = $"https://twitter.com/{username}",
                ProfileImageUrl = user.ProfileImageUrlFullSize.Replace("http://", "https://"),
                ProfileBackgroundImageUrl = user.ProfileBackgroundImageUrlHttps,
                ProfileBannerURL = user.ProfileBannerURL,
                Protected = user.Protected
            };
        }
    }
}