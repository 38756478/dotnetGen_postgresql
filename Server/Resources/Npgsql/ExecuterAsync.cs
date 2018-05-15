﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Npgsql {
	partial class Executer {
		async public Task ExecuteReaderAsync(Func<NpgsqlDataReader, Task> readerHander, CommandType cmdType, string cmdText, params NpgsqlParameter[] cmdParms) {
			DateTime dt = DateTime.Now;
			NpgsqlCommand cmd = new NpgsqlCommand();
			DateTime logtxt_dt = DateTime.Now;
			var pc = await PrepareCommandAsync(cmd, cmdType, cmdText, cmdParms);
			string logtxt = pc.logtxt;
			if (IsTracePerformance) logtxt += $"PrepareCommand: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms\r\n";
			Exception ex = null;
			try {
				if (IsTracePerformance) logtxt_dt = DateTime.Now;
				if (cmd.Connection.State == ConnectionState.Closed) await cmd.Connection.OpenAsync();
				if (IsTracePerformance) {
					logtxt += $"Open: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms\r\n";
					logtxt_dt = DateTime.Now;
				}
				using (NpgsqlDataReader dr = await cmd.ExecuteReaderAsync() as NpgsqlDataReader) {
					if (IsTracePerformance) logtxt += $"ExecuteReader: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms\r\n";
					while (true) {
						if (IsTracePerformance) logtxt_dt = DateTime.Now;
						bool isread = await dr.ReadAsync();
						if (IsTracePerformance) logtxt += $"	dr.Read: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms\r\n";
						if (isread == false) break;

						if (readerHander != null) {
							object[] values = null;
							if (IsTracePerformance) {
								logtxt_dt = DateTime.Now;
								values = new object[dr.FieldCount];
								for (int a = 0; a < values.Length; a++) values[a] = await dr.GetFieldValueAsync<object>(a);
								logtxt += $"	dr.GetValues: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms\r\n";
								logtxt_dt = DateTime.Now;
							}
							await readerHander(dr);
							if (IsTracePerformance) logtxt += $"	readerHander: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms ({string.Join(",", values)})\r\n";
						}
					}
					if (IsTracePerformance) logtxt_dt = DateTime.Now;
					dr.Close();
				}
				if (IsTracePerformance) logtxt += $"ExecuteReader_dispose: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms\r\n";
			} catch (Exception ex2) {
				ex = ex2;
			}

			if (IsTracePerformance) logtxt_dt = DateTime.Now;
			this.Pool.ReleaseConnection(pc.conn);
			if (IsTracePerformance) logtxt += $"ReleaseConnection: {DateTime.Now.Subtract(logtxt_dt).TotalMilliseconds}ms Total: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms";
			LoggerException(cmd, ex, dt, logtxt);
		}
		async public Task<object[][]> ExeucteArrayAsync(CommandType cmdType, string cmdText, params NpgsqlParameter[] cmdParms) {
			List<object[]> ret = new List<object[]>();
			await ExecuteReaderAsync(async dr => {
				object[] values = new object[dr.FieldCount];
				for (int a = 0; a < values.Length; a++) values[a] = await dr.GetFieldValueAsync<object>(a);
				ret.Add(values);
			}, cmdType, cmdText, cmdParms);
			return ret.ToArray();
		}
		async public Task<int> ExecuteNonQueryAsync(CommandType cmdType, string cmdText, params NpgsqlParameter[] cmdParms) {
			DateTime dt = DateTime.Now;
			NpgsqlCommand cmd = new NpgsqlCommand();
			var pc = await PrepareCommandAsync(cmd, cmdType, cmdText, cmdParms);
			int val = 0;
			Exception ex = null;
			try {
				if (cmd.Connection.State == ConnectionState.Closed) await cmd.Connection.OpenAsync();
				val = await cmd.ExecuteNonQueryAsync();
			} catch (Exception ex2) {
				ex = ex2;
			}

			this.Pool.ReleaseConnection(pc.conn);
			LoggerException(cmd, ex, dt, pc.logtxt);
			cmd.Parameters.Clear();
			return val;
		}
		async public Task<object> ExecuteScalarAsync(CommandType cmdType, string cmdText, params NpgsqlParameter[] cmdParms) {
			DateTime dt = DateTime.Now;
			NpgsqlCommand cmd = new NpgsqlCommand();
			var pc = await PrepareCommandAsync(cmd, cmdType, cmdText, cmdParms);
			object val = null;
			Exception ex = null;
			try {
				if (cmd.Connection.State == ConnectionState.Closed) await cmd.Connection.OpenAsync();
				val = await cmd.ExecuteScalarAsync();
			} catch (Exception ex2) {
				ex = ex2;
			}

			this.Pool.ReleaseConnection(pc.conn);
			LoggerException(cmd, ex, dt, pc.logtxt);
			cmd.Parameters.Clear();
			return val;
		}

		async private Task<(Connection2 conn, string logtxt)> PrepareCommandAsync(NpgsqlCommand cmd, CommandType cmdType, string cmdText, NpgsqlParameter[] cmdParms) {
			string logtxt = "";
			DateTime dt = DateTime.Now;
			cmd.CommandType = cmdType;
			cmd.CommandText = cmdText;

			if (cmdParms != null) {
				foreach (NpgsqlParameter parm in cmdParms) {
					if (parm == null) continue;
					if (parm.Value == null) parm.Value = DBNull.Value;
					cmd.Parameters.Add(parm);
				}
			}

			Connection2 conn = null;
			if (IsTracePerformance) logtxt += $"	PrepareCommand_part1: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms cmdParms: {cmdParms.Length}\r\n";

			if (IsTracePerformance) dt = DateTime.Now;
			conn = await this.Pool.GetConnectionAsync();
			cmd.Connection = conn.SqlConnection;
			if (IsTracePerformance) logtxt += $"	PrepareCommand_tran==null: {DateTime.Now.Subtract(dt).TotalMilliseconds}ms\r\n";

			return (conn, logtxt);
		}
	}
}