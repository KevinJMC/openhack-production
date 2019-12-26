﻿using AutoFixture;
using FakeItEasy;
using LinkyLink.Controllers;
using LinkyLink.Models;
using LinkyLink.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using Times = Moq.Times;

namespace LinkyLink.Tests
{
    public class LinksControllerTests
    {
        private readonly Mock<ILinksService> _mockService;
        private readonly LinksController _linksController;

        public LinksControllerTests()
        {
            _mockService = new Mock<ILinksService>();
            _linksController = new LinksController(_mockService.Object);
        }

        [Fact]
        public async Task GetLinkBundleReturnsNotFoundIfLinkBundleDoesntExists()
        {
            // Arrange 
            string vanityUrl = "samplelink";
            
            // Act
            ActionResult<LinkBundle> result = await _linksController.GetLinkBundle(vanityUrl);
           
            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetLinkBundleReturnsDocumentIfLinkBundleExists()
        {
            // Arrange 
            LinkBundle linkBundle = new LinkBundle
            {
                VanityUrl = "samplelink"
            };

            _mockService.Setup(service => service.FindLinkBundle(linkBundle.VanityUrl))
                .ReturnsAsync(linkBundle);

            // Act
            ActionResult<LinkBundle> result = await _linksController.GetLinkBundle(linkBundle.VanityUrl);

            // Assert
            Assert.IsType<LinkBundle>(result.Value);
        }

        [Fact]
        public async Task GetLinkBundlesForUserReturnsNotFoundIfLinkBundleDoesntExists()
        {
            // Arrange 
            string userId = "userhash";

            _mockService.Setup(service => service.GetUserAccountHash())
                .Returns("userhash");

            // Act
            ActionResult<LinkBundle> result = await _linksController.GetLinkBundlesForUser(userId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetLinkBundleForUserReturnsLinkBundles()
        {
            // Arrange
            string userId = "userhash";
            List<LinkBundle> linkBundles = new List<LinkBundle>
            {
                new LinkBundle
                {
                    UserId = userId,
                    VanityUrl = "samplelink",
                    Links = new List<Link>
                    {
                        new Link
                        {
                            Id = "sample"
                        }
                    }
                },                
                new LinkBundle
                {
                    UserId = userId,
                    VanityUrl = "samplelink1",      
                    Links = new List<Link>
                    {
                        new Link
                        {
                            Id = "sample1"
                        }
                    }
                }
            };
            
            _mockService.Setup(service => service.GetUserAccountHash())
                .Returns("userhash");

            _mockService.Setup(service => service.FindLinkBundlesForUser(userId))
                .ReturnsAsync(linkBundles);

            // Act
            ActionResult<LinkBundle> result = await _linksController.GetLinkBundlesForUser(userId);

            // Assert
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetLinkBundleForUserReturnsUnAuthorizedIfMissingAuth()
        {
            // Arrange
            LinkBundle linkBundle = new LinkBundle
            {
                UserId = "userhash",
                VanityUrl = "samplelink"
            };

            // Act
            ActionResult<LinkBundle> result = await _linksController.GetLinkBundlesForUser(linkBundle.UserId);

            // Assert
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task PostLinkBundleCreatesLinkBundleWhenValidPayload()
        {
            // Arrange
            LinkBundle expectedLinkBundle = null;

            _mockService.Setup(r => r.CreateLinkBundle(It.IsAny<LinkBundle>()))
                .Callback<LinkBundle>(x => expectedLinkBundle = x);
            
            LinkBundle linkBundle = new LinkBundle
            {
                Id = "userhash",
                VanityUrl = "samplelink",
                Description = "sampledescription",
                Links = new List<Link>    
                {    
                    new Link    
                    { 
                        Id = "sample"
                    }
                }
            };

            // Act
            ActionResult<LinkBundle> result = await _linksController.PostLinkBundle(linkBundle);

            // Assert
            _mockService.Verify(x => x.CreateLinkBundle(It.IsAny<LinkBundle>()), Times.Once);
            Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(expectedLinkBundle.Id, linkBundle.Id);
            Assert.Equal(expectedLinkBundle.UserId, linkBundle.UserId);
            Assert.Equal(expectedLinkBundle.VanityUrl, linkBundle.VanityUrl);
            Assert.Equal(expectedLinkBundle.Links.Count(), linkBundle.Links.Count);
        }

        [Fact]
        public async Task PostLinkBundlePopulatesVanityUrlIfNotProvided()
        {
            // Arrange
            LinkBundle expectedLinkBundle = null;

            LinkBundle linkBundle = new LinkBundle
            {
                Id = "userhash",
                VanityUrl = string.Empty,
                Links = new List<Link> { new Link() }
            };

            _mockService.Setup(r => r.CreateLinkBundle(It.IsAny<LinkBundle>()))
                .Callback<LinkBundle>(x => expectedLinkBundle = x);

            // Act
            ActionResult<LinkBundle> result = await _linksController.PostLinkBundle(linkBundle);

            // Assert
            Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.False(string.IsNullOrEmpty(expectedLinkBundle.VanityUrl));
        }

        [Theory]
        [InlineData("lower")]
        [InlineData("UPPER")]
        [InlineData("MiXEd")]
        public async Task PostLinkBundleConvertsVanityUrlToLowerCase(string vanityUrl)
        {
            // Arrange
            LinkBundle expectedLinkBundle = null;

            LinkBundle linkBundle = new LinkBundle
            {
                Id = "userhash",
                VanityUrl = vanityUrl,
                Links = new List<Link> { new Link() }
            };

            _mockService.Setup(r => r.CreateLinkBundle(It.IsAny<LinkBundle>()))
                .Callback<LinkBundle>(x => expectedLinkBundle = x);

            // Act
            ActionResult<LinkBundle> result = await _linksController.PostLinkBundle(linkBundle);

            // Assert
            Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(expectedLinkBundle.VanityUrl, vanityUrl.ToLower());
        }

        [Fact]
        public async Task DeleteLinkBundleReturnsUnAuthorizedIfMissingAuth()
        {
            // Arrange
            LinkBundle linkBundle = new LinkBundle
            {
                UserId = "userhash",
                VanityUrl = "samplelink"
            };

            // Act
            ActionResult<LinkBundle> result = await _linksController.DeleteLinkBundle(linkBundle.VanityUrl);

            // Assert
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task DeleteLinkBundleReturnsNotFoundIfLinkBundleDoesntExists()
        {
            // Arrange 
            string userId = "userhash";
            string vanityUrl = "sampleVanityUrl";

            _mockService.Setup(service => service.GetUserAccountHash())
                .Returns("userhash");

            // Act
            ActionResult<LinkBundle> result = await _linksController.DeleteLinkBundle(vanityUrl);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeleteLinkBundleReturnsForbiddenIfLinkBundleOwnedByOtherUser()
        {
            // Arrange 
            LinkBundle linkBundle = new LinkBundle
            {
                UserId = "userhash1",
                VanityUrl = "samplelink"
            };

            _mockService.Setup(service => service.GetUserAccountHash())
                .Returns("userhash");

            _mockService.Setup(service => service.FindLinkBundle(linkBundle.VanityUrl))
                .ReturnsAsync(linkBundle);

            // Act
            ActionResult<LinkBundle> result = await _linksController.DeleteLinkBundle(linkBundle.VanityUrl);

            // Assert
            Assert.IsType<ForbidResult>(result.Result);
        }
    }
}
