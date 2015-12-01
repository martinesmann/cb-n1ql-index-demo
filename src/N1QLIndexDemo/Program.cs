using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.N1QL;
using Newtonsoft.Json;

namespace N1QLIndexDemo
{
    class Program
    {
        static int numberOfTestDocuments = 100;//500 * 1000;
        static int batchSize = 1000;

        static string sqlCreateIndex1 =
            "CREATE INDEX `index_1` ON `default`(IndexedType) USING GSI";

        static string sqlCreateIndex2 =
            "CREATE INDEX `index_2` ON `default`(Month) USING GSI";

        static string sqlCreateIndex3 =
            "CREATE INDEX `index_3` ON `default`(Day) USING GSI";

        static string sqlSelectIndex1 =
            "SELECT * FROM system:indexes WHERE name='index_1'";

        static string sqlSelectIndex2 =
           "SELECT * FROM system:indexes WHERE name='index_2'";

        static string sqlSelectIndex3 =
           "SELECT * FROM system:indexes WHERE name='index_3'";

        static string sqlDeleteIndex1 =
            "DROP INDEX `default`.`index_1` USING GSI";

        static string sqlDeleteIndex2 =
            "DROP INDEX `default`.`index_2` USING GSI";

        static string sqlDeleteIndex3 =
            "DROP INDEX `default`.`index_3` USING GSI";

        static string sqlCountTestDocs =
            "SELECT COUNT(*) FROM `default` WHERE type = 'perfTest'";

        static string sqlDeleteTestDocs =
            "DELETE FROM `default` d WHERE d.type = 'perfTest' RETURNING d.Id";

        static string sqlSelect =
            "SELECT * FROM `default` WHERE IndexedType='person3' AND Month > 5 AND Day < 20";

        static List<string> executionTimes =
            new List<string>();

        static void Main(string[] args)
        {
            ClusterHelper.Initialize();

            Console.WriteLine("Clean-up started...");
            ResetCouchbaseServer();
            Console.WriteLine("Clean-up done");

            Console.WriteLine("Starting test...");

            Console.WriteLine("Creating test documents");
            GenerateDocuments();

            Console.WriteLine("Select without Index - Metrics:");
            Console.WriteLine(ExecuteSql(sqlSelect, label: "Query time without index", logtime: true));

            Console.WriteLine(ExecuteSql(sqlCreateIndex1, logtime: false));
            Console.WriteLine(ExecuteSql(sqlCreateIndex2, logtime: false));
            Console.WriteLine(ExecuteSql(sqlCreateIndex3, logtime: false));

            Console.WriteLine("Waiting 10s before runing tests...");
            Thread.Sleep(TimeSpan.FromSeconds(10));

            Console.WriteLine("Select with Index - Metrics:");
            Console.WriteLine(ExecuteSql(sqlSelect, label: "Query time with index", logtime: true));

            Console.WriteLine("Clean-up started...");
            ResetCouchbaseServer();
            Console.WriteLine("Clean-up done");

            Console.WriteLine("=========REPORT RESULT====== ");
            Console.WriteLine("Number of documents in the test: " + numberOfTestDocuments);
            Console.WriteLine("Execution Times--> ");
            Console.WriteLine(string.Join("\n", executionTimes));

            Console.WriteLine("Press 'Enter' to exit");
            Console.ReadLine();
        }

        private static void ResetCouchbaseServer()
        {
            int count = 0;
            do
            {
                ExecuteSql(sqlDeleteTestDocs);
                var result = ExecuteSql(sqlCountTestDocs);
                count = result.Exception != null || (result.Errors != null && result.Errors.Any()) 
                    ? 1 
                    : int.Parse(result.Rows.First()["$1"].Value.ToString()); ;

            } while (count > 0);

            ExecuteSql(sqlCreateIndex1, logtime: false);
            var i = ExecuteSql(sqlSelectIndex1);

            if (ExecuteSql(sqlSelectIndex1).Rows.Any())
            {
                ExecuteSql(sqlDeleteIndex1);
            }

            if (ExecuteSql(sqlSelectIndex2).Rows.Any())
            {
                ExecuteSql(sqlDeleteIndex2);
            }

            if (ExecuteSql(sqlSelectIndex3).Rows.Any())
            {
                ExecuteSql(sqlDeleteIndex3);
            }
        }

        private static IQueryResult<dynamic> ExecuteSql(string sql)
        {
            return ClusterHelper
                .GetBucket("default")
                .Query<dynamic>(new QueryRequest(sql).Timeout(TimeSpan.FromMinutes(5)));
        }

        private static string ExecuteSql(string sql, string label = null, bool logtime = false)
        {
            var response =
                ExecuteSql(sql);

            if (response.Errors != null && response.Errors.Any())
            {
                executionTimes.Add(label + ": Error" + response.Errors.First().Message);
            }
            else if (response.Exception != null)
            {
                executionTimes.Add(label + ": Exception" + response.Exception.Message);
            }
            else if (logtime)
            {
                executionTimes.Add(label + ": " + response.Metrics.ExecutionTime);
            }

            var res = JsonConvert.SerializeObject(
                new
                {
                    Sql = sql,
                    Metrics = response.Metrics,
                    Success = response.Success,
                    Result = response.Rows != null && response.Rows.Any() ? response.Rows.First() : "--",
                    Error = response.Errors != null && response.Errors.Any() ? response.Errors.First().Message : "--",
                }, Formatting.Indented);

            return res;
        }

        private static void GenerateDocuments()
        {
            int rounds = numberOfTestDocuments > batchSize ? numberOfTestDocuments / batchSize : 1;
            int testDocsPerLoop = rounds > 1 ? batchSize : numberOfTestDocuments;
            Random ran = new Random();

            for (int n = 0; n < rounds; n++)
            {
                var docs = new Dictionary<string, dynamic>();

                for (int i = 0; i < testDocsPerLoop; i++)
                {

                    string id = Guid.NewGuid().ToString();
                    string postFix = ran.Next(1, 4).ToString();
                    var doc = new
                    {
                        Id = id,
                        type = "perfTest",
                        IndexedType = "person" + postFix,
                        NoneIndexedType = "person" + postFix,
                        Day = ran.Next(1, 29),
                        Month = ran.Next(1, 12),
                        Year = "2015",
                        TextSmall = new string(
                            Enumerable.Range(0, ran.Next(100, 250)).Select(item => (char)ran.Next(44, 126)).ToArray()),
                        TextMedium = new string(
                            Enumerable.Range(0, ran.Next(200, 500)).Select(item => (char)ran.Next(44, 126)).ToArray()),
                        TextLarge = new string(
                            Enumerable.Range(0, ran.Next(700, 1000)).Select(item => (char)ran.Next(44, 126)).ToArray()),
                        TextExtraLarge = new string(
                            Enumerable.Range(0, ran.Next(1200, 1500)).Select(item => (char)ran.Next(44, 126)).ToArray())
                    };

                    docs.Add(id, doc);
                }

                var upsert =
                   ClusterHelper.GetBucket("default")
                   .Upsert<dynamic>(docs);

                Console.Write(".");
            }

        }
    }
}