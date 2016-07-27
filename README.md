My diagnostics tools
====================

It is a repository of small tools which might help you diagnose .NET and Windows applications. Some tools are still available on the old [Codeplex page](http://diagnettoolkit.codeplex.com/). Each project contains a `_binaries` folder with compiled version of the tool.

Following tools have been published:

### [Bazik (2.0)](Bazik) ###

It's a simple, but powerful, SQL Server monitor. Shows you all running requests, active transactions as well as transaction log usage. It also embeds some interesting SQL Server performance reports and, thanks to the [Justin Pealing's query plan drawer](https://github.com/JustinPealing/html-query-plan), draws nice query plans for the currently running queries. More information can be found in a post on my blog: <https://lowleveldesign.wordpress.com/2013/09/26/bazik-sql-server-monitoring-application>. Some improvements were made since then.

Download the binary release, change the connection string in the web.config and observe the data for your database.

### [windbgsvc](WinSvcDiagnostics) ###

A tool to debug a start of a Windows service, described in the blog post: <https://lowleveldesign.wordpress.com/2015/06/22/how-to-debug-windows-services-written-in-net-part-ii/>

### [inwrap](inwrap) ###

A tool to collect logs from a failing Windows service, described in the blog post: <https://lowleveldesign.wordpress.com/2014/11/30/how-to-debug-windows-services-written-in-net-part-i/>

### [MSMQ helper tools](msmq)

Three tools that will help you move MSMQ messages between computers: MessageDumper, MessagePeeker and MessagePusher. More information can be found on my blog: <http://lowleveldesign.wordpress.com/2014/04/06/msmq-helper-tools/>.

### [Process Governor](ProcessGovernor)

This application allows you to set a limit on a memory committed by a process. On Windows committed memory is actually all private memory that the process uses. I wrote this tool to test my .NET applications (including web applications) for memory leaks. With it I can check if under heavy load they won't throw OutOfMemoryException. For more information visit my blog: <http://lowleveldesign.wordpress.com/2013/11/21/set-process-memory-limit-with-process-governor>.

### [ADO.NET Trace Reader](AdoNetTraceReader)

AdoNetTraceReader is a parser for etw events stored in a xml file. In an installation package you can also find .reg files needed to enable ADO.NET tracing in a registry and a providers file for the logman tracing. For a complete example how to diagnose ADO.NET problems you may read my [blog post](http://lowleveldesign.wordpress.com/2012/09/07/diagnosing-ado-net-with-etw-traces). Just to catch up those are steps required to collect and parse ADO.NET traces:

1. setup (run only once): `setup-ado.net4x64.reg or setup-ado.net4x86.reg`, `mofcomp adonet` in the framework directory (c:\Windows\Microsoft.NET\Framework64\v4.0.30319\)
2. start trace collection: `logman start adonettrace -pf .\ctrl.guid.adonet -o adonettrace.etl -ets`
3. stop trace collection after performing some data tasks: `logman stop adonettrace -ets`
4. convert etl file to xml: `tracerpt -of xml .\adonettrace.etl`
5. parse and filter (if necessary) the xml file: `.\AdoNetTraceReader.exe -i dumpfile.xml -o parsed.txt -p 2008`
6. read logs and diagnose:)

### [FrontMan](FrontMan)

It's a simple application that can be used to diagnose other processes start-up - it logs the process command line and copies all files which paths are found in call arguments. If installed it can intercept a given process globally, logging each of its call. More on this tool as well as an example usage to diagnose ASP.NET compilation can be found on my blog: <http://lowleveldesign.wordpress.com/2013/01/25/diagnosing-aspnet-views-compilation>. Usage:

```
frontman <app-to-start> [args]
frontman -install <app-image-name>
frontman -uninstall <app-image-name>
```

### [ElmahLogViewer](ElmahLogViewer)

It's a small web application that can show Elmah logs from different applications. It's a sample I described on my blog: <http://lowleveldesign.wordpress.com/2013/03/24/elmah-axd-log-viewer-for-multiple-apps/>. To access the exception list open the `/elmah.axd?app=<your-app-name>`.

Note: the sample viewer uses a MySql database. In order to connect it to a different error log store you will need to make some modifications in code. Please use the Discussions tab if you have any problems or questions.
