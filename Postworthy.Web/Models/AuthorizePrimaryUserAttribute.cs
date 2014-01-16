using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using Postworthy.Models.Account;
using System.Web.Security;

namespace Postworthy.Web.Models
{
    public class AuthorizePrimaryUserAttribute : AuthorizeAttribute
    {
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            base.OnAuthorization(filterContext);

            if (filterContext.HttpContext.User.Identity.Name.ToLower() != UsersCollection.PrimaryUser().TwitterScreenName.ToLower())
            {
                FormsAuthentication.SignOut();
                filterContext.Result = new HttpUnauthorizedResult("Not Primary User");
            }
        }
    }
}