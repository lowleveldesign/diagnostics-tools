using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Caching;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Xsl;
using Dapper;
using Bazik.Models;
using System.Web;
using System.Configuration;

namespace Bazik.Controllers
{
    public class IndexRangerController : Controller
    {
        private const String BazikConnPrefix = "MsSqlConnString#";

        private class MsSqlSesssionEqualityComparer : IEqualityComparer<MsSqlSession>
        {
            public bool Equals(MsSqlSession x, MsSqlSession y)
            {
                if (x == y)
                {
                    return true;
                }
                if (x == null || y == null)
                {
                    return false;
                }
                return x.session_id.Equals(y.session_id);
            }

            public int GetHashCode(MsSqlSession obj)
            {
                return obj.session_id.GetHashCode();
            }
        }

        public ActionResult ShowDashboard(String serverName = null)
        {
            String connstr = null;
            if (serverName == null)
            {
                // use the first available connection string
                foreach (ConnectionStringSettings cs in WebConfigurationManager.ConnectionStrings)
                {
                    if (cs.Name.StartsWith(BazikConnPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        connstr = cs.ConnectionString;
                        serverName = cs.Name.Substring(BazikConnPrefix.Length);
                        break;
                    }
                }
            }
            else
            {
                connstr = WebConfigurationManager.ConnectionStrings[BazikConnPrefix + serverName].ConnectionString;
            }

            var result = new MsSqlDashboardModel { ServerName = serverName };

            using (var conn = new SqlConnection(connstr))
            {
                conn.Open();

                result.DatabaseNames = conn.Query<String>("select name from sys.databases where database_id > 4 order by name");

                result.ActiveRequests = conn.Query<MsSqlRequest>(
@"select req.request_id, 
    db_name(req.database_id) as database_name,
    req.start_time, req.total_elapsed_time, req.status, req.command,
    req.connection_id, req.session_id, req.blocking_Session_id,
    req.wait_type, req.transaction_id, req.percent_complete,
    req.cpu_time, req.reads, req.logical_reads, req.writes, 
    req.transaction_isolation_level,
    ses.host_name
from sys.dm_exec_requests req
    inner join sys.dm_exec_sessions ses on ses.session_id = req.session_id
    cross apply sys.dm_exec_sql_text(req.sql_handle)
where req.session_id <> @@spid
order by start_time asc");

                String heavyLoadKey = String.Format("_HeavyLoad#{0}", serverName);
                String activeTransactionsKey = String.Format("ActiveTransactions#{0}", serverName);
                String transactionLogKey = String.Format("TransactionLog#{0}", serverName);
                if (HttpRuntime.Cache[heavyLoadKey] == null)
                {
                    result.ActiveTransactions = HttpRuntime.Cache.Get(activeTransactionsKey) as IEnumerable<MsSqlTransaction>;
                    if (result.ActiveTransactions == null)
                    {
                        try
                        {
                            result.ActiveTransactions = conn.Query<MsSqlTransaction>(
                                @"select sess.session_id
      ,sess.host_name
      ,sess.program_name
      ,sess.login_name
      ,dt.transaction_id
      ,dt.database_transaction_begin_time
      ,dt.database_transaction_state
      ,cast(dt.database_transaction_log_record_count as bigint)
      ,dt.database_transaction_log_bytes_used
      ,dt.database_transaction_log_bytes_reserved
      ,dt.database_transaction_begin_lsn
      ,dt.database_transaction_last_lsn
      ,st.is_user_transaction
from sys.dm_tran_active_transactions at 
    join sys.dm_tran_database_transactions dt on at.transaction_id = dt.transaction_id
    join sys.dm_tran_session_transactions st on st.transaction_id = dt.transaction_id
    join sys.dm_exec_sessions sess on sess.session_id = st.session_id
order by dt.database_transaction_log_bytes_used desc", commandTimeout: 10);
                            // we will query transactions every 2 minutes
                            HttpRuntime.Cache.Insert(activeTransactionsKey, result.ActiveTransactions, null, DateTime.UtcNow.Add(TimeSpan.FromMinutes(2)), Cache.NoSlidingExpiration);
                        }
                        catch (SqlException)
                        {
                            // sql is under heavy load - let's give him some rest
                            ViewBag.HeavyLoad = true;
                            HttpRuntime.Cache.Insert(heavyLoadKey, true, null, DateTime.UtcNow.Add(TimeSpan.FromMinutes(4)), Cache.NoSlidingExpiration);
                        }
                    }
                    var transactionLogUsage = HttpRuntime.Cache.Get(transactionLogKey) as IEnumerable<MsSqlTransactionLogUsage>;
                    if (transactionLogUsage == null)
                    {
                        try
                        {
                            transactionLogUsage = conn.Query<MsSqlTransactionLogUsage>(
@"create table #t (database_name varchar(128), log_size_mb float, log_space_used_perc float, s int);
insert into #t 
exec ('DBCC SQLPERF(logspace)');
select * from #t where log_space_used_perc > 15 and database_name not in ('master', 'tempdb', 'msdb') order by log_space_used_perc desc;
drop table #t", commandTimeout: 10);
                            // we will query sessions every 10 minutes
                            HttpRuntime.Cache.Insert(transactionLogKey, transactionLogUsage, null, DateTime.UtcNow.Add(TimeSpan.FromMinutes(1)), Cache.NoSlidingExpiration);
                        }
                        catch (SqlException)
                        {
                            // sql is under heavy load - let's give him some rest
                            ViewBag.HeavyLoad = true;
                            HttpRuntime.Cache.Insert(heavyLoadKey, true, null, DateTime.UtcNow.Add(TimeSpan.FromMinutes(4)), Cache.NoSlidingExpiration);
                        }
                    }
                    result.TransactionLogUsage = transactionLogUsage;
                }
                else
                {
                    ViewBag.HeavyLoad = true;
                }
            }

            return View(result);
        }

        [HttpPost]
        public ActionResult ShowDashboard(String server, String db, String qtype)
        {
            if ("rp".Equals(qtype, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("MaintenanceReport", new { srv = server, db });
            }
            if ("si".Equals(qtype, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("IndexStats", new { srv = server, db });
            }
            return RedirectToAction("TopQueries", new { srv = server, db, qtype });
        }

        public new ActionResult Session(String srv, short sid)
        {
            ViewBag.srv = srv;
            using (var conn = new SqlConnection(WebConfigurationManager.ConnectionStrings["MsSqlConnString#" + srv].ConnectionString))
            {
                conn.Open();

                var result = conn.Query<MsSqlSession>(
                    @"select session_id, login_time, 
        (select count(1) from sys.dm_tran_session_transactions where session_id = s.session_id) as explicit_tran_num,
        (select count(1) from sys.dm_tran_locks where request_session_id = s.session_id) as locks_num,
        (select count(1) from sys.dm_tran_locks where request_status = 'WAIT' and request_session_id = s.session_id) as waiting_locks_num,
        host_name, program_name, login_name,
        status, cpu_time, memory_usage,
        reads, logical_reads, writes, transaction_isolation_level
    from sys.dm_exec_sessions s where 
        is_user_process = 1 and session_id = @sid",
                    new { sid }).FirstOrDefault();

                if (result == null)
                {
                    return HttpNotFound("Session not found. Probably finished.");
                }

                // requests
                result.Requests = conn.Query<MsSqlRequest>(
@"select req.request_id, 
    db_name(req.database_id) as database_name,
    req.start_time, req.total_elapsed_time, req.status, req.command,
    req.connection_id, req.session_id, req.blocking_Session_id,
    req.wait_type, req.transaction_id, req.percent_complete,
    req.cpu_time, req.reads, req.logical_reads, req.writes, 
    req.transaction_isolation_level,
    ses.host_name
from sys.dm_exec_requests req
    inner join sys.dm_exec_sessions ses on ses.session_id = req.session_id
    cross apply sys.dm_exec_sql_text(req.sql_handle)
where req.session_id = @sid
order by start_time asc", new { sid });

                // connections
                result.Connections = conn.Query<MsSqlConnection>(
@"select  c.session_id
       ,sq.text
       ,c.connect_time
       ,c.protocol_type
       ,c.num_reads
       ,c.num_writes
       ,c.client_net_address
       ,c.connection_id
       ,c.most_recent_session_id
from sys.dm_exec_connections c
    cross apply sys.dm_exec_sql_text(c.most_recent_sql_handle) sq
    where c.session_id in (@sid)
        or c.most_recent_session_id in (@sid)", new { sid });

                return View(result);
            }
        }

        public new ActionResult Request(String srv, short sid, short rid)
        {
            ViewBag.srv = srv;
            using (var conn = new SqlConnection(WebConfigurationManager.ConnectionStrings["MsSqlConnString#" + srv].ConnectionString))
            {
                conn.Open();

                var result = conn.Query<MsSqlRequest>(
@"select req.request_id, substring(text, (statement_start_offset/2) + 1,
    ((case statement_end_offset
        when -1
            then datalength(text)
        else statement_end_offset
    end - statement_start_offset)/2) + 1) as query_text,
    query_plan,
    db_name(req.database_id) as database_name,
    req.start_time, req.total_elapsed_time, req.status, req.command,
    req.connection_id, req.session_id, req.blocking_Session_id,
    req.wait_type, req.transaction_id, req.percent_complete,
    req.cpu_time, req.reads, req.logical_reads, req.writes, 
    req.transaction_isolation_level,
    ses.host_name
from sys.dm_exec_requests req
    inner join sys.dm_exec_sessions ses on ses.session_id = req.session_id
    cross apply sys.dm_exec_sql_text(req.sql_handle)
    cross apply sys.dm_exec_text_query_plan (req.plan_handle, default, default)
where req.request_id = @rid and req.session_id = @sid",
                    new { rid, sid }).FirstOrDefault();

                if (result == null)
                {
                    return HttpNotFound("Request not found. Probably finished.");
                }
                if (!String.IsNullOrEmpty(result.query_plan))
                {
                    // we will transform the xml query plan using google xslt and embed it into a web page
                    var myXslTrans = new XslCompiledTransform();
                    myXslTrans.Load(Server.MapPath("~/Scripts/qp.xslt"));

                    using (var xmlreader = new XmlTextReader(new StringReader(result.query_plan)))
                    {
                        var sw = new StringWriter();
                        myXslTrans.Transform(xmlreader, null, sw);
                        result.query_plan_html = sw.ToString();
                    }
                }

                return View(result);
            }
        }

        public ActionResult TopQueries(String qtype, String db, String srv)
        {
            using (var conn = new SqlConnection(WebConfigurationManager.ConnectionStrings["MsSqlConnString#" + srv].ConnectionString))
            {
                int top = 20;
                IEnumerable<MsSqlTopQuery> result = null;
                conn.Open();

                if ("ttio".Equals(qtype, StringComparison.OrdinalIgnoreCase))
                {
                    result = conn.Query<MsSqlTopQuery>(
@"SELECT TOP (@top)
            [average] = (total_logical_reads + total_logical_writes) / qs.execution_count
            ,[total] = (total_logical_reads + total_logical_writes)
            ,qs.execution_count
            ,[individual_query] = SUBSTRING (qt.text,qs.statement_start_offset/2, 
             (CASE WHEN qs.statement_end_offset = -1 
                THEN LEN(CONVERT(NVARCHAR(MAX), qt.text)) * 2 
              ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) 
            ,parent_query = qt.text
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) as qt
    WHERE qt.dbid = db_id(@db)
    ORDER BY [total] DESC", new { top, db });
                    ViewBag.unit = "IO";
                }
                else if ("ttcpu".Equals(qtype, StringComparison.OrdinalIgnoreCase))
                {
                    result = conn.Query<MsSqlTopQuery>(
@"SELECT TOP (@top)
            [average] = (total_worker_time) / qs.execution_count
            ,[total] = (total_worker_time)
            ,qs.execution_count
            ,[individual_query] = SUBSTRING (qt.text,qs.statement_start_offset/2, 
             (CASE WHEN qs.statement_end_offset = -1 
                THEN LEN(CONVERT(NVARCHAR(MAX), qt.text)) * 2 
              ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) 
            ,parent_query = qt.text
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) as qt
    WHERE qt.dbid = db_id(@db)
    ORDER BY [total] DESC", new { top, db });
                    ViewBag.unit = "CPU";

                }
                else if ("tttime".Equals(qtype, StringComparison.OrdinalIgnoreCase))
                {
                    result = conn.Query<MsSqlTopQuery>(
@"SELECT TOP (@top)
            [average] = (total_elapsed_time) / qs.execution_count
            ,[total] = (total_elapsed_time)
            ,qs.execution_count
            ,[individual_query] = SUBSTRING (qt.text,qs.statement_start_offset/2, 
             (CASE WHEN qs.statement_end_offset = -1 
                THEN LEN(CONVERT(NVARCHAR(MAX), qt.text)) * 2 
              ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) 
            ,parent_query = qt.text
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) as qt
    WHERE qt.dbid = db_id(@db)
    ORDER BY [total] DESC", new { top, db });
                    ViewBag.unit = "Time";
                }
                else if ("taio".Equals(qtype, StringComparison.OrdinalIgnoreCase))
                {
                    result = conn.Query<MsSqlTopQuery>(
@"SELECT TOP (@top)
            [average] = (total_logical_reads + total_logical_writes) / qs.execution_count
            ,[total] = (total_logical_reads + total_logical_writes)
            ,qs.execution_count
            ,[individual_query] = SUBSTRING (qt.text,qs.statement_start_offset/2, 
             (CASE WHEN qs.statement_end_offset = -1 
                THEN LEN(CONVERT(NVARCHAR(MAX), qt.text)) * 2 
              ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) 
            ,parent_query = qt.text
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) as qt
    WHERE qt.dbid = db_id(@db)
    ORDER BY [average] DESC", new { top, db });
                    ViewBag.unit = "IO";
                }
                else if ("tacpu".Equals(qtype, StringComparison.OrdinalIgnoreCase))
                {
                    result = conn.Query<MsSqlTopQuery>(
@"SELECT TOP (@top)
            [average] = (total_worker_time) / qs.execution_count
            ,[total] = (total_worker_time)
            ,qs.execution_count
            ,[individual_query] = SUBSTRING (qt.text,qs.statement_start_offset/2, 
             (CASE WHEN qs.statement_end_offset = -1 
                THEN LEN(CONVERT(NVARCHAR(MAX), qt.text)) * 2 
              ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) 
            ,parent_query = qt.text
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) as qt
    WHERE qt.dbid = db_id(@db)
    ORDER BY [average] DESC", new { top, db });
                    ViewBag.unit = "CPU";
                }
                else if ("tatime".Equals(qtype, StringComparison.OrdinalIgnoreCase))
                {
                    result = conn.Query<MsSqlTopQuery>(
@"SELECT TOP (@top)
            [average] = (total_elapsed_time) / qs.execution_count
            ,[total] = (total_elapsed_time)
            ,qs.execution_count
            ,[individual_query] = SUBSTRING (qt.text,qs.statement_start_offset/2, 
             (CASE WHEN qs.statement_end_offset = -1 
                THEN LEN(CONVERT(NVARCHAR(MAX), qt.text)) * 2 
              ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) 
            ,parent_query = qt.text
    FROM sys.dm_exec_query_stats qs
    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) as qt
    WHERE qt.dbid = db_id(@db)
    ORDER BY [average] DESC", new { top, db });
                    ViewBag.unit = "Time";
                }

                return View(result);
            }
        }

        public ActionResult IndexStats(String srv, String db, bool recalculate = false)
        {
            var result = new MsSqlDatabaseAnalysis {
                db = db,
                srv = srv
            };

            var builder = new SqlConnectionStringBuilder(WebConfigurationManager.ConnectionStrings["MsSqlConnString#" + srv].ConnectionString);
            builder.InitialCatalog = db;

            using (var conn = new SqlConnection(builder.ToString()))
            {
                conn.Open();

                result.UnusedIndexes = conn.Query<MsSqlDatabaseAnalysis.UnusedIndex>(
@"with   calced
as     (select [object_id],
               index_id,
               user_seeks + user_scans + user_lookups as reads,
               user_updates as writes,
               convert (decimal (10, 2), user_updates * 100.0 / (user_seeks + user_scans + user_lookups + user_updates)) as perc
        from   sys.dm_db_index_usage_stats
        where  database_id = DB_ID())
select case
when reads = 0 and writes = 0 then 'Consider dropping : not used at all'
when reads = 0 and writes > 0 then 'Consider dropping : only writes'
when writes > reads then 'Consider dropping : more writes (' + RTRIM(perc) + '% of activity)'
when reads = writes then 'Reads and writes equal'
end as [status],
       object_name(i.[object_id])  as [table], 
       i.Name as [index],
       c.reads,
       c.writes,
       i.is_disabled
from   calced as c
       inner join
       sys.indexes as i
       on c.[object_id] = i.[object_id]
          and c.index_id = i.index_id
where  c.writes >= c.reads and i.Name is not null
order by [table], writes");

                var cacheKey = String.Format("index-stats:{0}:{1}", srv, db);
                var cachedStats = HttpContext.Cache.Get(cacheKey) as Tuple<DateTime, IEnumerable<MsSqlDatabaseAnalysis.IndexPhysicalStats>>;
                if (recalculate)
                {
                    result.IndexesPhysicalStats = conn.Query<MsSqlDatabaseAnalysis.IndexPhysicalStats>(
@"SELECT
  OBJECT_NAME (ips.[object_id]) AS [table],
  si.name AS [index],
  si.type_desc as index_type_desc,
  ips.index_depth,
  ROUND (ips.avg_fragmentation_in_percent, 2) AS avg_fragmentation_in_percent,
  ips.avg_fragment_size_in_pages,
  ips.page_count AS page_count,
  ROUND (ips.avg_page_space_used_in_percent, 2) AS avg_page_space_used_in_percent,
  ips.record_count,
  ips.alloc_unit_type_desc,
  ips.avg_record_size_in_bytes
FROM sys.dm_db_index_physical_stats (
  DB_ID(),
  NULL,
  NULL,
  NULL,
  null) ips
CROSS APPLY sys.indexes si
WHERE
  si.object_id = ips.object_id
  AND si.index_id = ips.index_id
  AND ips.index_level = 0 order by avg_fragmentation_in_percent desc", commandTimeout: 0);
                    result.LastStatsDate = DateTime.Now;
                    HttpContext.Cache.Insert(cacheKey, new Tuple<DateTime, IEnumerable<MsSqlDatabaseAnalysis.IndexPhysicalStats>>(
                                                result.LastStatsDate.Value, result.IndexesPhysicalStats));
                }
                else if (cachedStats != null)
                {
                    result.LastStatsDate = cachedStats.Item1;
                    result.IndexesPhysicalStats = cachedStats.Item2;
                }
                else
                {
                    result.IndexesPhysicalStats = new MsSqlDatabaseAnalysis.IndexPhysicalStats[0];
                }

                return View(result);
            }
        }
    }
}
