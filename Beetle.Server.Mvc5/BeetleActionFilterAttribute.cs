using Beetle.Server.Mvc.Properties;
using System;
using System.Collections.Specialized;
using System.Web;
using System.Web.Mvc;

namespace Beetle.Server.Mvc {

    /// <summary>
    /// Imitate OData behavior for MVC Controllers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class BeetleActionFilterAttribute : ActionFilterAttribute {
        private BeetleConfig _beetleConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="BeetleActionFilterAttribute" /> class.
        /// </summary>
        /// <param name="configType">Type of the config.</param>
        /// <exception cref="System.ArgumentException">Cannot create config instance.</exception>
        public BeetleActionFilterAttribute(Type configType = null) {
            if (configType != null) {
                _beetleConfig = Activator.CreateInstance(configType) as BeetleConfig;
                if (_beetleConfig == null)
                    throw new ArgumentException(Resources.CannotCreateConfigInstance);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BeetleActionFilterAttribute"/> class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public BeetleActionFilterAttribute(BeetleConfig config) {
            _beetleConfig = config;
        }

        /// <summary>
        /// Called by the ASP.NET MVC framework before the action method executes.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
        /// <exception cref="BeetleException">BeetleActionFilterAttribute should only be applied to Queryable actions.</exception>
        public override void OnActionExecuting(ActionExecutingContext filterContext) {
            base.OnActionExecuting(filterContext);

            var controller = filterContext.Controller;
            var action = filterContext.ActionDescriptor;
            var reflectedAction = action as ReflectedActionDescriptor;
            var actionMethod = reflectedAction != null 
                ? reflectedAction.MethodInfo 
                : controller.GetType().GetMethod(action.ActionName);

            // do not do anything for action results
            if (typeof(ActionResult).IsAssignableFrom(actionMethod.ReturnType)) return;

            var service = controller as IBeetleService;
            if (_beetleConfig == null)
                _beetleConfig = service != null ? service.BeetleConfig : BeetleConfig.Instance;

            NameValueCollection queryParams;
            object[] actionArgs;
            // handle request message
            GetParameters(filterContext, out queryParams, out actionArgs);

            // execute the action method
            var contentValue = actionMethod.Invoke(controller, actionArgs);
            // process the request and return the result
            var actionContext = new ActionContext(action.ActionName, contentValue, queryParams, MaxResultCount);
            var processResult = ProcessRequest(contentValue, actionContext, service);
            // handle response message
            filterContext.Result = HandleResponse(filterContext, processResult);
        }

        /// <summary>
        /// Handles the request.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
        /// <param name="queryParams">The query params.</param>
        /// <param name="actionArgs">The action args.</param>
        /// <returns>When request is a valid beetle call, return true, otherwise false. If returned false, beetle won't call action method.</returns>
        protected virtual void GetParameters(ActionExecutingContext filterContext, out NameValueCollection queryParams, out object[] actionArgs) {
            var request = HttpContext.Current.Request;
            var actionParameters = filterContext.ActionDescriptor.GetParameters();
            Helper.GetParameters(out queryParams, out actionArgs, _beetleConfig, request, actionParameters, filterContext.ActionParameters);
        }

        /// <summary>
        /// Processes the request.
        /// </summary>
        /// <param name="contentValue">The content value.</param>
        /// <param name="actionContext">The action context.</param>
        /// <param name="service">The service.</param>
        /// <returns></returns>
        protected virtual ProcessResult ProcessRequest(object contentValue, ActionContext actionContext, IBeetleService service) {
            return service != null
                ? service.ProcessRequest(contentValue, actionContext)
                : Helper.ProcessRequest(contentValue, actionContext);
        }

        /// <summary>
        /// Handles the response.
        /// </summary>
        /// <param name="filterContext">The filter context.</param>
        /// <param name="result">The result.</param>
        /// <returns></returns>
        protected virtual ActionResult HandleResponse(ActionExecutingContext filterContext, ProcessResult result) {
            return Helper.HandleResponse(result, _beetleConfig);
        }

        /// <summary>
        /// Gets the beetle config.
        /// </summary>
        /// <value>
        /// The beetle config.
        /// </value>
        protected BeetleConfig BeetleConfig {
            get { return _beetleConfig; }
        }

        /// <summary>
        /// Gets or sets the maximum result count.
        /// </summary>
        /// <value>
        /// The maximum result count.
        /// </value>
        public int MaxResultCount { get; set; }
    }
}