﻿using LinkyLink.Models;
using LinkyLink.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LinkyLink.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    /// <summary>
    /// This class handles API requests for Link bundles and defines set of actions to peform on Link bundles.
    /// </summary>
    public class LinksController : ControllerBase
    {
        private readonly ILinksService _linksService;

        public LinksController(ILinksService linksService)
        {
            _linksService = linksService;
        }

        // Todo - Remove this method
        // GET: api/Links
        [HttpGet]
        public async Task<IEnumerable<LinkBundle>> GetLinkBundles()
        {
            return await _linksService.AllLinkBundles();
        }

        // GET: api/Links/{vanityUrl}
        [HttpGet("{vanityUrl}")]
        public async Task<ActionResult<LinkBundle>> GetLinkBundle(string vanityUrl)
        {
            var linkBundle = await _linksService.FindLinkBundle(vanityUrl);

            if (linkBundle == null)
            {
                return NotFound();
            }

            return linkBundle;
        }

        // GET: api/Links/User/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<LinkBundle>> GetLinkBundlesForUser(string userId)
        {
            string userHandle = _linksService.GetUserAccountHash();

            if (string.IsNullOrEmpty(userHandle) || userHandle != userId)
            {
                return Unauthorized();
            }

            var linkBundlesForUser = await _linksService.FindLinkBundlesForUser(userId);

            if (!linkBundlesForUser.Any())
            {
                return NotFound();
            }

            var results = linkBundlesForUser.Select(d => new
            {
                userId = d.UserId,
                vanityUrl = d.VanityUrl,
                description = d.Description,
                linkCount = d.Links.Count.ToString()
            });
            return new OkObjectResult(results);
        }

        // POST: api/Links
        [HttpPost]
        public async Task<ActionResult<LinkBundle>> PostLinkBundle(LinkBundle linkBundle)
        {
            if (linkBundle.Links.Count() == 0)
            {
                var problemDetails = new ProblemDetails()
                {
                    Title = "Payload is invalid",
                    Detail = "No links are provided",
                    Status = StatusCodes.Status400BadRequest,
                    Type = "/linkylink/clientissue",
                    Instance = Request.Path
                };

                return new BadRequestObjectResult(problemDetails);
            }

            string userHandle = _linksService.GetUserAccountHash();
            linkBundle.UserId = userHandle;

            ValidateVanityUrl(linkBundle);

            string vanity_regex = @"^([\w\d-])+(/([\w\d-])+)*$";
            Match match = Regex.Match(linkBundle.VanityUrl, vanity_regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return new BadRequestResult();
            }

            try
            {
                await _linksService.CreateLinkBundle(linkBundle);
            }
            catch (DbUpdateException)
            {
                if (await _linksService.LinkBundleExists(linkBundle.Id))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception)
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return CreatedAtAction("GetLinkBundle", new { vanityUrl = linkBundle.VanityUrl }, linkBundle);
        }

        // DELETE: api/Links/{vanityUrl}
        [HttpDelete("{vanityUrl}")]
        public async Task<ActionResult<LinkBundle>> DeleteLinkBundle(string vanityUrl)
        {
            string userHandle = _linksService.GetUserAccountHash();

            if (string.IsNullOrEmpty(userHandle))
            {
                return Unauthorized();
            }

            var linkBundle = await _linksService.FindLinkBundle(vanityUrl);

            if (linkBundle == null)
            {
                return NotFound();
            }

            if (!userHandle.Equals(linkBundle.UserId, StringComparison.InvariantCultureIgnoreCase))
            {
                return Forbid();
            }

            try
            {
                await _linksService.RemoveLinkBundle(linkBundle);
            }
            catch (Exception)
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return NoContent();
        }

        // PATCH: api/Links/{vanityUrl}
        [HttpPatch("{vanityUrl}")]
        public async Task<ActionResult<LinkBundle>> PatchLinkBundle(string vanityUrl, JsonPatchDocument<LinkBundle> linkBundle)
        {
            string userHandle = _linksService.GetUserAccountHash();
            if (string.IsNullOrEmpty(userHandle))
            {
                return Unauthorized();
            }

            var linkBundleEntry = await _linksService.FindLinkBundle(vanityUrl);

            if (linkBundleEntry == null)
            {
                return NotFound();
            }

            if (!userHandle.Equals(linkBundleEntry.UserId, StringComparison.InvariantCultureIgnoreCase))
            {
                return Forbid();
            }

            try
            {
                linkBundle.ApplyTo(linkBundleEntry, ModelState);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                await _linksService.UpdateLinkBundle(linkBundleEntry);
            }
            catch (Exception)
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return NoContent();
        }

        private void ValidateVanityUrl(LinkBundle linkDocument)
        {
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            
            if (string.IsNullOrWhiteSpace(linkDocument.VanityUrl))
            {
                var code = new char[7];
                var rng = new RNGCryptoServiceProvider();

                var bytes = new byte[sizeof(uint)];
                for (int i = 0; i < code.Length; i++)
                {
                    rng.GetBytes(bytes);
                    uint num = BitConverter.ToUInt32(bytes, 0) % (uint)characters.Length;
                    code[i] = characters[(int)num];
                }

                linkDocument.VanityUrl = new String(code);
            }

            // force lowercase
            linkDocument.VanityUrl = linkDocument.VanityUrl.ToLower();
        }
    }
}
