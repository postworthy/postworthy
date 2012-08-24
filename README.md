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

The project is also mobile ready right out of the box to see this visit the demo link above from your smartphone (iOS or Android).

##Project Features
==================================

* C# MVC3 (Razor View Engine)
* Mobile Ready
* Automatic Content Ranking
* NoSql
* Memcached
* Twitter API (REST & Streaming)
* Real Time Client Updates (Using SignalR)
* Runs on Mono (the demo is running on Mono)

##Tools
==================================

If you are not familiar with C# then you will want to have a look at these free development tools

#####Windows

<a href="http://www.microsoft.com/visualstudio/en-us/products/2010-editions/express">Visual Studio 2010 Express</a>
<br/>
<a href="http://www.couchbase.com/memcached">Couchbase</a> (in memcached mode)

#####Mac & Linux

<a href="http://monodevelop.com/">MonoDevelop</a>
<br/>
<a href="http://www.couchbase.com/memcached">Couchbase</a> (in memcached mode)


##Before You Build
==================================

Before you build you will want to sign up for a twitter account and sign up for a <a href="https://dev.twitter.com/">twitter api key</a>. 
Postworthy uses the twitter API to find content that you share and also to find content shared by those you follow. 
You will also want to authorize your twitter account to be used by your application.

#####Twitter Keys

Before you run the application for the first time you will need to sign up for the twitter developer api.
You will need to use the example.web.config file in the Postworthy.Web project to create a web.config
and also use the example.app.config in the UpdateRepository, Grouping, and Streaming projects to create 
app.config files for each.

You will then need to authorize your twitter account and use the  example.userscollection.config 
file in the Postworthy.Web directory to create a userscollection.config file.

Once you have your twitter account ready you will want to modify the PrimaryUser in both the web.config and 
the app.configs.

#####UsersCollection References

In both the web.config and the app.configs you will need to modify the UsersCollection setting to point 
to your UsersCollection file. You will also need to make sure that the process running your web application 
has both read and write access to this file.

#####Streaming API

If you chose to use the streaming service you will want to place your account information in the app.config 
file for the Postworthy.Tasks.Streaming project so that it can connect to the twitter stream. You will also want to update 
the PushURL so that it is pointing to your web applications streaming endpoint (i.e. - http://postworthy.com/streaming).

#####Memcached

You will need to have a local memcached instance running on your development machine. Postworthy is a NoSQL project
and Memcached allows for scalability as well as a speedy place to store frequently used data. By default Postworthy expects 
the memcached instance to be running local and be available on port 11211.

If you use Couchbase make sure that you configure it to run as a Memcached server.

##Software Licensing Policy
==================================

#####For Open Source Projects

If you are developing and distributing open source applications under the GPL License, then you are free to use Postworthy under the GPL License.
<a href="http://www.gnu.org/licenses/gpl-faq.html">GPL FAQ</a>

#####Commercial, Enterprise and Government Projects

Contact me at Landon.Key@gmail.com for more information on Commercial, Enterprise, and Government use of the Postworthy tools.