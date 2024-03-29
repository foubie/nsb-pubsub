﻿using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Persistence.Sql;
using NServiceBus.Transport.SQLServer;

public static class Program
{
    static async Task Main()
    {
        Console.Title = "Samples.Sql.Receiver";

        #region ReceiverConfiguration

        var endpointConfiguration = new EndpointConfiguration("Samples.Sql.Receiver");
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.EnableInstallers();
        var connection = @"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=NsbSampleSql;Integrated Security=SSPI;MultipleActiveResultSets=true;";


        var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connection);
        transport.DefaultSchema("receiver");
        transport.UseSchemaForQueue("error", "dbo");
        transport.UseSchemaForQueue("audit", "dbo");
        transport.UseSchemaForQueue("Samples.Sql.Sender", "sender");
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        transport.UseNativeDelayedDelivery().DisableTimeoutManagerCompatibility();

        var routing = transport.Routing();
        routing.RouteToEndpoint(typeof(OrderAccepted), "Samples.Sql.Sender");
        routing.RegisterPublisher(typeof(OrderSubmitted).Assembly, "Samples.Sql.Sender");

        var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
        var dialect = persistence.SqlDialect<SqlDialect.MsSqlServer>();
        dialect.Schema("receiver");
        persistence.ConnectionBuilder(
            connectionBuilder: () =>
            {
                return new SqlConnection(connection);
            });
        persistence.TablePrefix("");
        var subscriptions = persistence.SubscriptionSettings();
        subscriptions.CacheFor(TimeSpan.FromMinutes(1));

        var defaultFactory = LogManager.Use<DefaultFactory>();
        defaultFactory.Level(LogLevel.Debug);

        #endregion

        SqlHelper.CreateSchema(connection, "receiver");
        var allText = File.ReadAllText("Startup.sql");
        SqlHelper.ExecuteSql(connection, allText);
        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}