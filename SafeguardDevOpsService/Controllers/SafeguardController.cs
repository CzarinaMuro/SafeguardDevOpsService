﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using OneIdentity.DevOps.Attributes;
using OneIdentity.DevOps.Data;
using OneIdentity.DevOps.Data.Spp;
using OneIdentity.DevOps.Logic;
using A2ARetrievableAccount = OneIdentity.DevOps.Data.Spp.A2ARetrievableAccount;

namespace OneIdentity.DevOps.Controllers
{
    [ApiController]
    [Route("service/devops/[controller]")]
    public class SafeguardController : ControllerBase
    {
        private readonly Serilog.ILogger _logger;

        public SafeguardController()
        {
            _logger = Serilog.Log.Logger;
        }

        /// <summary>
        /// Get the state of the Safeguard appliance that the DevOps service is currently using.
        /// </summary>
        /// <response code="200">Success</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet]
        public ActionResult<SafeguardConnection> GetSafeguard([FromServices] ISafeguardLogic safeguard)
        {
            var safeguardConnection = safeguard.GetSafeguardConnection();

            return Ok(safeguardConnection);
        }

        /// <summary>
        /// Set the connection information for the Safeguard appliance that the DevOps service should use.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [UnhandledExceptionError]
        [HttpPut]
        public ActionResult<SafeguardConnection> SetSafeguard([FromServices] ISafeguardLogic safeguard,
            [FromBody] SafeguardData safeguardData)
        {
            var appliance = safeguard.SetSafeguardData(safeguardData);

            return Ok(appliance);
        }

        // /// <summary>
        // /// Deletes the current Safeguard configuration so that none is in use with the DevOps service.
        // /// </summary>
        // /// <response code="204">Success</response>
        // [UnhandledExceptionError]
        // [HttpDelete]
        // public ActionResult DeleteSafeguard([FromServices] ISafeguardLogic safeguard)
        // {
        //     safeguard.DeleteSafeguardData();
        //     return NoContent();
        //     // TODO: error handling?
        // }

        /// <summary>
        /// Get the current DevOps service configuration.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("Configuration")]
        public ActionResult<ServiceConfiguration> GetDevOpsConfiguration([FromServices] ISafeguardLogic safeguard)
        {
            var serviceConfiguration = safeguard.GetDevOpsConfiguration();

            return Ok(serviceConfiguration);
        }

        /// <summary>
        /// Invoke the Safeguard configuration of the A2A registration and A2A certificate user given a client certificate.
        /// The client certificate that will be used to create the A2A user in Safeguard and referenced by the A2A registration, can be
        /// uploaded as part of the this /Configuration endpoint or can be uploaded separately in the POST /ClientCertificate endpoint.
        /// If the client certificate was already uploaded using the POST /ClientCertificate endpoint, it does not need to be provided as part of this endpoint.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("Configuration")]
        public ActionResult<ServiceConfiguration> ConfigureSafeguard([FromServices] ISafeguardLogic safeguard, ClientCertificate certFile = null)
        {
            if (certFile?.Base64CertificateData != null)
            {
                safeguard.InstallClientCertificate(certFile);
            }

            var devOpsConfiguration = safeguard.ConfigureDevOpsService();

            return Ok(devOpsConfiguration);
        }

        /// <summary>
        /// Delete the DevOps service configuration.  This endpoint includes removing all account mappings, removing the A2A registration, A2A user and trusted
        /// certificate from Safeguard and removing all stored configuration in the DevOps service.
        /// </summary>
        /// <response code="204">No Content</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("Configuration")]
        public ActionResult<ServiceConfiguration> DeleteSafeguardConfiguration([FromServices] ISafeguardLogic safeguard)
        {
            safeguard.ConfigureDevOpsService();

            return NoContent();
        }

        /// <summary>
        /// Logon to the DevOps service.  The Authorization header should contain a valid Safeguard token.  This token can be acquired by
        /// logging into the Safeguard appliance using the Safeguard-ps command 'Connect-Safeguard -NoSessionVariable' and providing valid
        /// login credentials. A successful authentication will respond with a sessionKey that should be provided as a cookie for all
        /// subsequent endpoint calls.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        /// <response code="401">Unauthorized</response>
        [SafeguardTokenAuthorization]
        [UnhandledExceptionError]
        [HttpGet("Logon")]
        public ActionResult<SafeguardConnection> GetSafeguardLogon([FromServices] ISafeguardLogic safeguard)
        {
            var availability = safeguard.GetSafeguardConnection();
            if (availability == null)
                return NotFound("No Safeguard has not been configured");

            return Ok(availability);
        }

        /// <summary>
        /// Logoff the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("Logoff")]
        public ActionResult<SafeguardConnection> GetSafeguardLogoff([FromServices] ISafeguardLogic safeguard)
        {
            var sessionKey = HttpContext.Items["session-key"].ToString();
            AuthorizedCache.Instance.Remove(sessionKey);
//TODO: Remove the connection Context from the authorizedCache

            return Ok();
        }

        /// <summary>
        /// Get the information about the currently installed A2A client certificate.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("ClientCertificate")]
        public ActionResult<ClientCertificate> GetClientCertificate([FromServices] ISafeguardLogic safeguard)
        {
            var certificate = safeguard.GetClientCertificate();
            if (certificate == null)
                return NotFound();

            return Ok(certificate);
        }

        /// <summary>
        /// Upload an A2A client certificate.  This can be either a PFX format certificate that includes a private key or a signed certificate
        /// that was issued from a CSR.  (See GET /CSR). A client certificate must be uploaded before calling the POST /Configure endpoint.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("ClientCertificate")]
        public ActionResult InstallClientCertificate([FromServices] ISafeguardLogic safeguard, ClientCertificate certFile)
        {
            safeguard.InstallClientCertificate(certFile);
            return Ok();
        }

        /// <summary>
        /// Remove the installed A2A client certificate.
        /// </summary>
        /// <response code="204">No Content</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("ClientCertificate")]
        public ActionResult RemoveClientCertificate([FromServices] ISafeguardLogic safeguard)
        {
            safeguard.RemoveClientCertificate();

            return NoContent();
        }

        /// <summary>
        /// Get a CSR that can be signed and uploaded back to the DevOps service. If the issuer of the signed certificate is part of a certificate chain
        /// the certificate chain must be manually added as trusted certificates in the Safeguard appliance.
        /// </summary>
        /// <param name="size">Size of the certificate</param>
        /// <param name="subjectName">Subject name of the certificate</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("CSR")]
        public ActionResult<string> GetClientCsr([FromServices] ISafeguardLogic safeguard, [FromQuery] int? size, [FromQuery] string subjectName)
        {
            var csr = safeguard.GetClientCSR(size, subjectName);
            return Ok(csr);
        }

        /// <summary>
        /// Get all of the available Safeguard accounts for the currently logged in user, that can be mapped vault plugins.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("AvailableAccounts")]
        public ActionResult<IEnumerable<SppAccount>> GetAvailableAccounts([FromServices] ISafeguardLogic safeguard)
        {
            var availableAccounts = safeguard.GetAvailableAccounts();

            return Ok(availableAccounts);
        }

        /// <summary>
        /// Get the A2A registration that was created and used by the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="400">Bad Request</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("A2ARegistration")]
        public ActionResult<A2ARegistration> GetA2ARegistration([FromServices] ISafeguardLogic safeguard)
        {
            var registration = safeguard.GetA2ARegistration();
            if (registration == null)
                return NotFound();

            return Ok(registration);
        }

        /// <summary>
        /// Delete the A2A registration that is being used by the DevOps service. To help prevent unintended removal of the A2A registration,
        /// A2A user and trusted client certificate as well as removal of the account mappings, the confirm query param is required. 
        /// </summary>
        /// <param name="confirm">This query parameter must be set to "yes" if the caller intends to remove the A2A registration.</param>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpDelete("A2ARegistration")]
        public ActionResult<A2ARegistration> DeleteA2ARegistration([FromServices] ISafeguardLogic safeguard, [FromQuery] string confirm)
        {
            if (confirm == null || !confirm.Equals("yes", StringComparison.InvariantCultureIgnoreCase))
                return BadRequest();

            safeguard.DeleteA2ARegistration();

            return NoContent();
        }

        /// <summary>
        /// Get a list of the retrievable accounts that are associated with the A2A registration that is being used by the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpGet("A2ARegistration/RetrievableAccounts")]
        public ActionResult<IEnumerable<A2ARetrievableAccount>> GetRetrievableAccounts([FromServices] ISafeguardLogic safeguard)
        {
            var retrievableAccounts = safeguard.GetA2ARetrievableAccounts();

            return Ok(retrievableAccounts);
        }

        /// <summary>
        /// Add a set of accounts as retrievable accounts associated with the A2A registration that is being used by the DevOps service.
        /// </summary>
        /// <response code="200">Success</response>
        /// <response code="404">Not found</response>
        [SafeguardSessionKeyAuthorization]
        [UnhandledExceptionError]
        [HttpPost("A2ARegistration/RetrievableAccounts")]
        public ActionResult<IEnumerable<A2ARetrievableAccount>> AddRetrievableAccounts([FromServices] ISafeguardLogic safeguard, IEnumerable<SppAccount> accounts)
        {
            var retrievableAccounts = safeguard.AddA2ARetrievableAccounts(accounts);

            return Ok(retrievableAccounts);
        }

    }
}