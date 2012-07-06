#Before You Build

You will want to install Nuget if you dont have it because there are some references used in the project
that can easily be installed using Nuget.

#Twitter Keys

Before you run the application for the first time you will need to sign up for the twitter developer api.
You will need to use the example.web.config file in the Postworthy.Web project to create a web.config
and also use the example.app.config in the UpdateRepository project to create an app.config file.

You will then need to authorize your twitter account and use the  example.userscollection.config 
file in the Postworthy.Web directory to create a userscollection.config file.

Once you have your twitter account ready you will want to modify the PrimaryUser in both the web.config and 
the app.config.

#UsersCollection References

In both the web.config and the app.config you will need to modify the UsersCollection setting to point 
to your UsersCollection file.

#Memcached

You will need to have a local memcached instance running on your development machine.
