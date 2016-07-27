using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Bazik.Models
{
    public class MsSqlDatabaseAnalysis
    {
        public class UnusedIndex
        {
            public String status;
            public String table;
            public String index;
            public long reads;
            public long writes;
            public bool is_disabled;
        }

        public class IndexPhysicalStats
        {
            public String table;
            public String index;
            public String index_type_desc;
            public byte index_depth;
            public double avg_fragmentation_in_percent;
            public double avg_fragment_size_in_pages;
            public long page_count;
            public double avg_page_space_used_in_percent;
            public long record_count;
            public double avg_record_size_in_bytes;
            public String alloc_unit_type_desc;
        }

        public String srv;
        public String db;

        public IEnumerable<UnusedIndex> UnusedIndexes { get; set; }

        public DateTime? LastStatsDate { get; set; }
        public IEnumerable<IndexPhysicalStats> IndexesPhysicalStats { get; set; }
    }

    public class MsSqlDashboardModel
    {
        public String ServerName { get; set; }

        public IEnumerable<MsSqlTransactionLogUsage> TransactionLogUsage { get; set; }

        public IEnumerable<MsSqlRequest> ActiveRequests { get; set; }

        public IEnumerable<MsSqlTransaction> ActiveTransactions { get; set; }

        public IEnumerable<String> DatabaseNames { get; set; }
    }

    public class MsSqlConnection
    {
        public int session_id;
        public String text;
        public DateTime connect_time;
        public String protocol_type;
        public int num_reads;
        public int num_writes;
        public String client_net_address;
        public Guid connection_id;
        public int most_recent_session_id;
    }

    public class MsSqlMaintenanceCommand
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public String ObjectName { get; set; }

        public String IndexName { get; set; }

        public byte IndexType { get; set; }

        public String ExtendedInfo { get; set; }

        public String Command { get; set; }

        public int ErrorNumber { get; set; }

        public String ErrorMessage { get; set; }

        public Guid? TraceId { get; set; }
    }

    public class MsSqlRequest
    {
        public int request_id;
        public String query_text;
        public String query_plan;
        public byte[] plan_handle;
        public String database_name;
        public DateTime start_time;
        public int total_elapsed_time;
        public String status;
        public String command;
        public Guid connection_id;
        public short session_id;
        public short blocking_session_id;
        public String wait_type;
        public long transaction_id;
        public Single percent_complete;
        public int cpu_time;
        public long reads;
        public long logical_reads;
        public long writes;
        public short transaction_isolation_level;
        public String user;
        public String host_name;

        public String query_plan_html;

        // required by snapshots
        public DateTime snapshot_time;
    }
    
    public sealed class MsSqlSession
    {
        public short session_id;
        public DateTime login_time;
        public int request_num;
        public int explicit_tran_num;
        public int locks_num;
        public int waiting_locks_num;
        public String host_name;
        public String program_name;
        public String login_name;
        public String status;
        public int cpu_time;
        public int memory_usage;
        public long reads;
        public long logical_reads;
        public long writes;
        public short transaction_isolation_level;

        public IEnumerable<MsSqlRequest> Requests { get; set; }

        public IEnumerable<MsSqlConnection> Connections { get; set; }
    }
    
    public class MsSqlTopQuery
    {
        public long average;
        public long total;
        public long execution_count;
        public String individual_query;
        public String parent_query;
    }
    
    public class MsSqlTransaction
    {
        public short session_id;
        public bool is_user_transaction;
        public String host_name;
        public String program_name;
        public String login_name;
        public long transaction_id;
        public DateTime? database_transaction_begin_time;
        public int database_transaction_state;
        public long database_transaction_log_record_count;
        public long database_transaction_log_bytes_used;
        public long database_transaction_log_bytes_reserved;
        public decimal database_transaction_begin_lsn;
        public decimal database_transaction_last_lsn;
    }

    public class MsSqlTransactionLogUsage
    {
        public String database_name;
        public double log_size_mb;
        public double log_space_used_perc;
    }

    public class DbSizeLog
    {
        public String Name { get; set; }
        public float RowSizeMB { get; set; }
        public float TotalSizeMB { get; set; }
        public float TextIndexSizeMB { get; set; }
        public float LogSizeMB { get; set; }
        public float LogSizeUsedMB { get; set; }
        public DateTime InsertionDateUtc { get; set; }
    }
}