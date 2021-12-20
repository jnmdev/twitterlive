﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub.Converters;
using BirdsiteLive.Common.Regexes;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Models;
using BirdsiteLive.Models.WellKnownModels;
using BirdsiteLive.Twitter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BirdsiteLive.Controllers
{
    [ApiController]
    public class WellKnownController : ControllerBase
    {
        private readonly IModerationRepository _moderationRepository;
        private readonly ITwitterUserService _twitterUserService;
        private readonly ITwitterUserDal _twitterUserDal;
        private readonly InstanceSettings _settings;

        #region Ctor
        public WellKnownController(InstanceSettings settings, ITwitterUserService twitterUserService, ITwitterUserDal twitterUserDal, IModerationRepository moderationRepository)
        {
            _twitterUserService = twitterUserService;
            _twitterUserDal = twitterUserDal;
            _moderationRepository = moderationRepository;
            _settings = settings;
        }
        #endregion

        [Route("/.well-known/nodeinfo")]
        public IActionResult WellKnownNodeInfo()
        {
            var nodeInfo = new WellKnownNodeInfo
            {
                links = new Link[]
                {
                    new Link()
                    {
                        rel = "http://nodeinfo.diaspora.software/ns/schema/2.0",
                        href = $"https://{_settings.Domain}/nodeinfo/2.0.json"
                    },
                    new Link()
                    {
                        rel = "http://nodeinfo.diaspora.software/ns/schema/2.1",
                        href = $"https://{_settings.Domain}/nodeinfo/2.1.json"
                    }
                }
            };
            return new JsonResult(nodeInfo);
        }

        [Route("/nodeinfo/{id}.json")]
        public async Task<IActionResult> NodeInfo(string id)
        {
            var version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(3);
            var twitterUsersCount = await _twitterUserDal.GetTwitterUsersCountAsync();
            var isOpenRegistration = _moderationRepository.GetModerationType(ModerationEntityTypeEnum.Follower) != ModerationTypeEnum.WhiteListing;

            if (id == "2.0")
            {
                var nodeInfo = new NodeInfoV20
                {
                    version = "2.0",
                    usage = new Usage()
                    {
                        localPosts = 0,
                        users = new Users()
                        {
                            total = twitterUsersCount
                        }
                    },
                    software = new Software()
                    {
                        name = "birdsitelive",
                        version = version
                    },
                    protocols = new[]
                    {
                        "activitypub"
                    },
                    openRegistrations = isOpenRegistration,
                    services = new Models.WellKnownModels.Services()
                    {
                        inbound = new object[0],
                        outbound = new object[0]
                    },
                    metadata = new Metadata()
                    {
                        email = _settings.AdminEmail
                    }
                };
                return new JsonResult(nodeInfo);
            }
            if (id == "2.1")
            {
                var nodeInfo = new NodeInfoV21
                {
                    version = "2.1",
                    usage = new Usage()
                    {
                        localPosts = 0,
                        users = new Users()
                        {
                            total = twitterUsersCount
                        }
                    },
                    software = new SoftwareV21()
                    {
                        name = "birdsitelive",
                        version = version,
                        repository = "https://github.com/NicolasConstant/BirdsiteLive"
                    },
                    protocols = new[]
                    {
                        "activitypub"
                    },
                    openRegistrations = isOpenRegistration,
                    services = new Models.WellKnownModels.Services()
                    {
                        inbound = new object[0],
                        outbound = new object[0]
                    },
                    metadata = new Metadata()
                    {
                        email = _settings.AdminEmail
                    }
                };
                return new JsonResult(nodeInfo);
            }

            return NotFound();
        }

        [Route("/.well-known/webfinger")]
        public IActionResult Webfinger(string resource = null)
        {
            var acct = resource.Split("acct:")[1].Trim();

            string name = null;
            string domain = null;

            var splitAcct = acct.Split('@', StringSplitOptions.RemoveEmptyEntries);

            var atCount = acct.Count(x => x == '@');
            if (atCount == 1 && acct.StartsWith('@'))
            {
                name = splitAcct[1];
            }
            else if (atCount == 1 || atCount == 2)
            {
                name = splitAcct[0];
                domain = splitAcct[1];
            }
            else
            {
                return BadRequest();
            }

            // Ensure lowercase
            name = name.ToLowerInvariant();

            // Ensure valid username 
            // https://help.twitter.com/en/managing-your-account/twitter-username-rules
            if (string.IsNullOrWhiteSpace(name) || !UserRegexes.TwitterAccount.IsMatch(name) || name.Length > 15 )
                return NotFound();

            if (!string.IsNullOrWhiteSpace(domain) && domain != _settings.Domain)
                return NotFound();

            var user = _twitterUserService.GetUser(name);
            if (user == null)
                return NotFound();

            var actorUrl = UrlFactory.GetActorUrl(_settings.Domain, name);

            var result = new WebFingerResult()
            {
                subject = $"acct:{name}@{_settings.Domain}",
                aliases = new[]
                {
                    actorUrl
                },
                links = new List<WebFingerLink>
                {
                    new WebFingerLink()
                    {
                        rel = "http://webfinger.net/rel/profile-page",
                        type = "text/html",
                        href = actorUrl
                    },
                    new WebFingerLink()
                    {
                        rel = "self",
                        type = "application/activity+json",
                        href = actorUrl
                    }
                }
            };

            return new JsonResult(result);
        }
    }
}