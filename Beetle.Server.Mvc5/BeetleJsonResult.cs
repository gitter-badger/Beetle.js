using Beetle.Server.Mvc.Properties;
using System;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace Beetle.Server.Mvc {

    /// <summary>
    /// Beetle Json result to modify default Json serialization.
    /// </summary>
    public class BeetleJsonResult: JsonResult {
        private readonly BeetleConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="BeetleJsonResult"/> class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public BeetleJsonResult(BeetleConfig config) {
            _config = config;
        }

        /// <summary>
        /// Enables processing of the result of an action method by a custom type that inherits from the <see cref="T:System.Web.Mvc.ActionResult" /> class.
        /// </summary>
        /// <param name="context">The context within which the result is executed.</param>
        public override void ExecuteResult(ControllerContext context) {
            var response = context.HttpContext.Response;

            if (JsonRequestBehavior == JsonRequestBehavior.DenyGet && context.HttpContext.Request.HttpMethod == "GET")
                throw new InvalidOperationException(Resources.GETRequestNotAllowed);

            response.ContentType = !string.IsNullOrEmpty(ContentType) 
                ? ContentType 
                : "application/json";

            if (ContentEncoding != null)
                response.ContentEncoding = ContentEncoding;

            if (Data != null)
                response.Write(JsonConvert.SerializeObject(Data, _config.JsonSerializerSettings));
        }
    }
}