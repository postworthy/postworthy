using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using Postworthy.Models.Account;
using System.Web.Security;

namespace Postworthy.Web.Bot.Models
{
    public class AuthorizePrimaryUserAttribute : AuthorizeAttribute
    {
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            base.OnAuthorization(filterContext);



            if (UsersCollection.PrimaryUsers()
                .Where(u=>u.TwitterScreenName.ToLower() == filterContext.HttpContext.User.Identity.Name.ToLower())
                .Where(u=>u.IsPrimaryUser)
                .Count() == 0)
            {
                FormsAuthentication.SignOut();
                filterContext.Result = new HttpUnauthorizedResult("Not Primary User");
            }
        }
    }
}