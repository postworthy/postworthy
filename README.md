##Project Demo
==================================

The best way to explain what Postworthy is all about is to <a href="http://postworthy.com">experience it first hand</a>.

##Project Intro
==================================

If you are the type of person who loves to discover amazing content from across the web and share it with your friends
then postworthy is for you. Postworthy allows you to create a website filled with the things you find and the things 
found by those you follow on twitter.

Postworthy has been created in C# and has been designed to run on multiple operating systems 
(postworthy.com is actually a linux server).

##Tools
==================================
If you are not familiar with C# then you will want to have a look at these free development tools

###Windows

<a href="http://www.microsoft.com/visualstudio/en-us/products/2010-editions/express">Visual Studio 2010 Express</a>
<a href="http://www.couchbase.com/memcached">Couchbase</a>

###Mac & Linux

<a href="http://monodevelop.com/">MonoDevelop</a>
<a href="http://www.couchbase.com/memcached">Couchbase</a>


##Before You Build
==================================
Before you build you will want to sign up for a twitter account and sign up for a <a href="https://dev.twitter.com/">twitter api key</a>. 
Postworthy uses the twitter API to find content that you share and also to find content shared by those you follow. 
You will also want to authorize your twitter account to be used by your application.

###Twitter Keys

Before you run the application for the first time you will need to sign up for the twitter developer api.
You will need to use the example.web.config file in the Postworthy.Web project to create a web.config
and also use the example.app.config in the UpdateRepository project to create an app.config file.

You will then need to authorize your twitter account and use the  example.userscollection.config 
file in the Postworthy.Web directory to create a userscollection.config file.

Once you have your twitter account ready you will want to modify the PrimaryUser in both the web.config and 
the app.config.

###UsersCollection References

In both the web.config and the app.config you will need to modify the UsersCollection setting to point 
to your UsersCollection file.

###Memcached

You will need to have a local memcached instance running on your development machine. Postworthy is a NoSQL project
and Memcached allows for scalability as well as a speedy place to store frequently used data. By default Postworthy expects 
the memcached instance to be running local and be available on port 11211.