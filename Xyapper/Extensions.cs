﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Xyapper.Internal;

namespace Xyapper
{
    /// <summary>
    /// Xyapper main extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Get list of objects from database query
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection">DB Connection</param>
        /// <param name="command">DB Command to execute</param>
        /// <param name="transaction">Transaction to use</param>
        /// <returns></returns>
        public static IEnumerable<T> XQuery<T>(this IDbConnection connection, IDbCommand command, IDbTransaction transaction = null) where T : new()
        {
            connection.OpenIfNot();
            command.Connection = connection;
            command.Transaction = transaction;

            LogCommand(command);

            using (var reader = command.ExecuteReader())
            {
                var deserializer = DeserializerFactory.GetDeserializer<T>(reader);

                while (reader.Read())
                {
                    yield return deserializer(reader);
                }
            }
        }

        /// <summary>
        /// Get list of objects from database query
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection">DB Connection</param>
        /// <param name="commandText">Plain command text</param>
        /// <param name="parameterSet">Anonymous type object with parameters</param>
        /// <param name="transaction">Transaction to use</param>
        /// <returns></returns>
        public static IEnumerable<T> XQuery<T>(this IDbConnection connection, string commandText, object parameterSet = null, IDbTransaction transaction = null) where T : new()
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = commandText;
                command.Transaction = transaction;

                AddParameters(command, parameterSet);

                foreach(var item in connection.XQuery<T>(command, transaction))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Get list of objects from database stored procedure
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection">DB Connection</param>
        /// <param name="procedureName">Stored procedure name</param>
        /// <param name="parameterSet">Anonymous type object with parameters</param>
        /// <param name="transaction">Transaction to use</param>
        /// <returns></returns>
        public static IEnumerable<T> XQueryProcedure<T>(this IDbConnection connection, string procedureName, object parameterSet = null, IDbTransaction transaction = null) where T : new()
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = procedureName;
                command.Transaction = transaction;

                AddParameters(command, parameterSet);

                foreach(var item in connection.XQuery<T>(command, transaction))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Execute command with no return data
        /// </summary>
        /// <param name="connection">DB Connection</param>
        /// <param name="commandText">Plain command text</param>
        /// <param name="parameterSet">Anonymous type object with parameters</param>
        /// <param name="transaction">Transaction to use</param>
        public static void XExecuteNonQuery(this IDbConnection connection, string commandText, object parameterSet = null, IDbTransaction transaction = null)
        {
            connection.OpenIfNot();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = commandText;
                command.Transaction = transaction;

                AddParameters(command, parameterSet);

                command.Connection = connection;

                LogCommand(command);

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Execute command with no return data
        /// </summary>
        /// <param name="connection">DB Connection</param>
        /// <param name="command">DB Command to execute</param>
        /// <param name="transaction">Transaction to use</param>
        public static void XExecuteNonQuery(this IDbConnection connection, IDbCommand command, IDbTransaction transaction = null)
        {
            connection.OpenIfNot();
            command.Connection = connection;
            command.Transaction = transaction;

            LogCommand(command);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Get DataTable from DB
        /// </summary>
        /// <param name="connection">DB Connection</param>
        /// <param name="command">DB Command to execute</param>
        /// <param name="transaction">Transaction to use</param>
        /// <returns></returns>
        public static DataTable XGetDataTable(this IDbConnection connection, IDbCommand command, IDbTransaction transaction = null)
        {
            connection.OpenIfNot();
            command.Connection = connection;
            command.Transaction = transaction;

            LogCommand(command);
            using (var reader = command.ExecuteReader())
            {
                return ReadDataTable(reader);
            }
        }

        /// <summary>
        /// Get DataTable from DB
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="commandText">Plain command text</param>
        /// <param name="parameterSet">Anonymous type object with parameters</param>
        /// <param name="transaction">Transaction to use</param>
        /// <returns></returns>
        public static DataTable XGetDataTable(this IDbConnection connection, string commandText, object parameterSet = null, IDbTransaction transaction = null)
        {
            connection.OpenIfNot();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = commandText;
                command.Transaction = transaction;

                AddParameters(command, parameterSet);
                command.Connection = connection;

                LogCommand(command);

                using (var reader = command.ExecuteReader())
                {
                    return ReadDataTable(reader);
                }
            }
        }

        /// <summary>
        /// Add parameters to command from anonymous type
        /// </summary>
        /// <param name="command"></param>
        /// <param name="parameterSet"></param>
        private static void AddParameters(IDbCommand command, object parameterSet)
        {
            if (parameterSet == null) return;

            var fields = parameterSet.GetType().GetProperties();

            foreach(var field in fields)
            {
                var parameter = command.CreateParameter();

                parameter.ParameterName = field.Name;
                parameter.Value = field.GetValue(parameterSet);

                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Read data to DataTable from IDataReader
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static DataTable ReadDataTable(IDataReader reader)
        {
            var schemaColumns = SchemaItem.FromDataTable(reader.GetSchemaTable());

            var result = new DataTable();
            foreach(var column in schemaColumns)
            {
                result.Columns.Add(column.ColumnName, column.DataType);
            }

            foreach(var rowArray in ReadRowArray(reader, schemaColumns.Length))
            {
                result.Rows.Add(rowArray);
            }

            return result;
        }

        /// <summary>
        /// Read a collections of arrays of objects from IDataReader
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        private static IEnumerable<object[]> ReadRowArray(IDataReader reader, int columns)
        {
            while (reader.Read())
            {
                var rowArray = new object[columns];
                reader.GetValues(rowArray);
                yield return rowArray;
            }
        }

        /// <summary>
        /// Open a connection of it is not open
        /// </summary>
        /// <param name="connection"></param>
        private static void OpenIfNot(this IDbConnection connection)
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }
        }

        /// <summary>
        /// Log command to a logging provider
        /// </summary>
        /// <param name="command"></param>
        private static void LogCommand(IDbCommand command)
        {
            if (!XyapperManager.EnableLogging) return;

            XyapperManager.Logger.Log(XyapperManager.CommandLogLevel, new EventId(), command, null, (cmd, exception) => 
            {
                var message = command.CommandText;
                if (command.Parameters.Count > 0)
                {
                    message += $"\r\nPARAMETERS: \r\n{string.Join("\r\n", command.Parameters.Cast<IDbDataParameter>().Select(parameter => $"{parameter.ParameterName} = '{parameter.Value.ToString()}'"))}";
                }
                return message;
            });
        }
    }
}
