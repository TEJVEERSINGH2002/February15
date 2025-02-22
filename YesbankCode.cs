using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Linq;
using static Yesbank.Controllers.HomeController;

namespace Yesbank.Controllers
{
    public class HomeController : Controller
    {
        string result1;
        private object reqHdr;
        private object reqBody;
        private static readonly HttpClient client = new HttpClient();
        string RequesterID = "";
        string ServiceName = "";
        string ReqRefNum = "";
        string ReqRefTimeStamp = "";
        string ServiceVersionNo = "";
        string accountNo = "";
        string pageSize = "";
        string pageNo = "";
        string startDate = "";
        string endDate = "";
        private readonly string connectionString = "Data Source=localhost;Initial Catalog=TSSWU12345;Persist Security Info=True;User ID=sa;Password=sa;TrustServerCertificate=True;";
        [HttpPost]
        public async Task<ContentResult> YesbankPost(string customerId, string username, string password)
        {
            Request.InputStream.Position = 0;
            string requestBody = "ram";
            using (var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }
            var json = JsonConvert.DeserializeObject(requestBody);
            var data = JsonConvert.DeserializeObject<Dictionary<string, AcctStatementInquiryReq>>(requestBody);
            var reqBody = data["ReqBody"];
            string accessToken = await GetOAuthToken(customerId, username, password);
            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("Failed to obtain access token");
                return Content("Failed to obtain access token");
            }
            string AdhocStatement = await GetStatement(accessToken, requestBody);
            Console.WriteLine("All Order Response: " + AdhocStatement);
            return Content(AdhocStatement);
        }
        private async Task<string> GetStatement(string accessToken, string requestBody)
        {
            string certificatePath = @"c:\ssl\pfx1234.pfx"; // Change to your .pfx file path
            string certificatePassword = "1234"; // Password for the .pfx file
            var certificate = new X509Certificate2(certificatePath, certificatePassword);

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            var client = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://uatskyway.yesbank.in/app/uat/AdhocStatement/V2/Inquiry");
            request.Headers.Add("X-IBM-Client-Id", "6422a38fb2235c3cfd0d49c5076207ad");
            request.Headers.Add("X-IBM-Client-Secret", "7ed640cb44d9c362483027fa093fdcd0");
            request.Headers.Add("token", accessToken);
            var data = JsonConvert.DeserializeObject<Dictionary<string, ReqBody>>(requestBody);
            var reqBody = data["ReqBody"];
            var jsonBody = new
            {
                AcctStatementInquiryReq = new
                {
                    ReqHdr = new
                    {
                        ConsumerContext = new
                        {
                            RequesterID = "APP"
                        },
                        ServiceContext = new
                        {
                            ServiceName = "AcctStatementInquiry",
                            ReqRefNum = "100112305573300",
                            ReqRefTimeStamp = "2023-04-12T16:30:15",
                            ServiceVersionNo = "1.0"
                        }
                    },
                    ReqBody = new
                    {
                        customerId = reqBody.CustomerId,
                        accountNo = reqBody.AccountNo,
                        pageSize = reqBody.PageSize,
                        pageNo = reqBody.PageNo,
                        startDate = reqBody.StartDate,
                        endDate = reqBody.EndDate
                    }
                }
            };
            var jsonString = JsonConvert.SerializeObject(jsonBody);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var AdhocStatement = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                var Statement = ReadDataFromJson(AdhocStatement);

                SaveOrderDetails(Statement.Tables["ResBody"], Statement.Tables["xfaceTransactionDetailsDTO"]);
                //var a=0;
            }
            else
            {
                return (AdhocStatement);
            }
            return AdhocStatement;
        }
        private async Task<string> GetOAuthToken(string customerId, string username, string password)
        {


            string certificatePath = @"c:\ssl\pfx1234.pfx"; // Change to your .pfx file path
            string certificatePassword = "1234"; // Password for the .pfx file
            var certificate = new X509Certificate2(certificatePath, certificatePassword);

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            using (var client = new HttpClient(handler))
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://uatskyway.yesbank.in/app/uat/AdhocStatement/V2/token");
                request.Headers.Add("X-IBM-Client-Id", "6422a38fb2235c3cfd0d49c5076207ad");
                request.Headers.Add("X-IBM-Client-Secret", "7ed640cb44d9c362483027fa093fdcd0");
                request.Headers.Add("Cookie", "ak_bmsc=EB9F3526575FBA25EDDC6DBBBC1C3D50~000000000000000000000000000000~YAAQ1QFAF8Mf5MGSAQAA9ej12xknfzuSHYswHZMJo2xxosrJwH/RHqK2mW1DGG3kiBKCEz5OXsedKbOHZ9//yzldV6xg1Nlwvbtvnx+8ksnBB08HP1T1P/ZxbAm17BiqD+YpcxTmuv+f6D+VyQ14V0ZtMBXD/afx43+ZUjA+0phcAdkbQFeisW6blXa7gP7zxRQd0Bk9OLSbj8hUgJUUMALBM8ifEfLg4U9GfbZNb6z+2B95fzsVh+ZN0I1ywIknPJ4Gg8AcPZJ2wJP9YUVBUQprVUz5Oq7+xOWEhKQny8oBFEMI9L6dHL4LHyHEtovRdWA0NjW9cdE+PU2f1ilIpHUD4j+rn55bRMw=");

                var jsonBody = new
                {
                    customerId = customerId,
                    username = username,
                    password = password
                };
                var jsonString = JsonConvert.SerializeObject(jsonBody);
                var content = new StringContent(jsonString, null, "application/json");
                request.Content = content;

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(result);
                result1 = jsonResponse["token"].ToString();
            }
            return (result1);
        }
        public DataSet ReadDataFromJson(string jsonString, XmlReadMode mode = XmlReadMode.Auto)
        {
            var originaljson = jsonString;
            //// Note:Json convertor needs a json with one node as root
            jsonString = $"{{ \"rootNode\": {{{jsonString.Trim().TrimStart('{').TrimEnd('}')}}} }}";
            //// Now it is secure that we have always a Json with one node as root 
            ///
            XmlDocument xd;
            try
            {
                xd = JsonConvert.DeserializeXmlNode(jsonString);
            }
            catch (Exception eee)
            {
                jsonString = $"{{ \"rootNode\": {{{originaljson.Trim().TrimStart('{').TrimEnd('}')}}} }}" + "}";
                xd = JsonConvert.DeserializeXmlNode(jsonString);

            }

            //// DataSet is able to read from XML and return a proper DataSet
            var result = new DataSet();
            result.ReadXml(new XmlNodeReader(xd), mode);
            return result;
        }
        private void SaveOrderDetails(DataTable ResBody, DataTable xfaceTransactionDetailsDTO)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    InsertDataIntoAccountStatement(ResBody, conn);

                    InsertDataIntoTransactionDetails(xfaceTransactionDetailsDTO, ResBody, conn);



                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while saving order details: {ex.Message}");
            }
        }
        private void InsertDataIntoAccountStatement(DataTable resBody, SqlConnection conn)
        {
            foreach (DataRow row in resBody.Rows)
            {
                StringBuilder columns = new StringBuilder();
                StringBuilder values = new StringBuilder();

                foreach (DataColumn column in resBody.Columns)
                {
                    if (columns.Length > 0)
                    {
                        columns.Append(", ");
                        values.Append(", ");
                    }

                    columns.Append(column.ColumnName);
                    values.Append("@").Append(column.ColumnName);
                }

                string query = $"INSERT INTO TYESAS ({columns}) VALUES ({values})";

                using (SqlCommand command = new SqlCommand(query, conn))
                {
                    foreach (DataColumn column in resBody.Columns)
                    {
                        command.Parameters.AddWithValue($"@{column.ColumnName}", row[column.ColumnName]);
                    }

                    command.ExecuteNonQuery();
                }
            }
        }
        private void InsertDataIntoTransactionDetails(DataTable xfaceTransactionDetailsDTO, DataTable ResBody, SqlConnection conn)
        {
            foreach (DataRow row in xfaceTransactionDetailsDTO.Rows)
            {
                StringBuilder columns = new StringBuilder();
                StringBuilder values = new StringBuilder();
                string accountNo = string.Empty;
                if (ResBody.Columns.Contains("ACCOUNTNO"))
                {
                    if (ResBody.Rows.Count > 0)
                    {
                        accountNo = ResBody.Rows[0]["ACCOUNTNO"].ToString();
                    }
                }
                columns.Append("ACCOUNTNO");
                values.Append("@ACCOUNTNO");
                foreach (DataColumn column in xfaceTransactionDetailsDTO.Columns)
                {
                    columns.Append(", ");
                    values.Append(", ");
                    columns.Append(column.ColumnName);
                    values.Append("@").Append(column.ColumnName);
                }
                string query = $"INSERT INTO TYESTRN ({columns}) VALUES ({values})";
                using (SqlCommand command = new SqlCommand(query, conn))
                {
                    command.Parameters.AddWithValue("@ACCOUNTNO", accountNo);
                    foreach (DataColumn column in xfaceTransactionDetailsDTO.Columns)
                    {
                        command.Parameters.AddWithValue($"@{column.ColumnName}", row[column.ColumnName]);
                    }
                    command.ExecuteNonQuery();
                }
            }
        }

        [HttpPost]
        public async Task<ContentResult> Yesbankpayment(string username, string password)
        {
            Request.InputStream.Position = 0;
            string requestBody = "ram";
            using (var reader = new StreamReader(HttpContext.Request.InputStream, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }
            var json = JsonConvert.DeserializeObject(requestBody);
            string certificatePath = @"c:\ssl\pfx1234.pfx"; // Change to your .pfx file path
            string certificatePassword = "1234"; // Password for the .pfx file
            var certificate = new X509Certificate2(certificatePath, certificatePassword);

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            var client = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://uatskyway.yesbank.in/app/uat/api-banking/v2.0/file-payments");
            request.Headers.Add("X-IBM-Client-Id", "6422a38fb2235c3cfd0d49c5076207ad");
            request.Headers.Add("X-IBM-Client-Secret", "7ed640cb44d9c362483027fa093fdcd0");
            request.Headers.Add("Authorization", "Basic dGVzdHVzZXI6VGlFc2JudHBAMTNOMjI=");
            request.Headers.Add("Cookie", "ak_bmsc=77D1C3E9EB601A1BE53058362384E9C2~000000000000000000000000000000~YAAQ5wFAF3fyGtSSAQAAz1az9Rl2c4am0n9ABurZ3FO4PUQZHGS7G04fhoAVZbNA3zdSF9wAmL19yzFWXxjgPqH24L532Prh5+oQiP9PUZDMo+cc0qqQ8jaGEBJgTZwUWDPxkwOFVdLUI7MeuhvWs1jA8k8MzvirnUxYsOvrguce7pXVF829jjUh25VDgpnEGXy10Fe0qaFEC5DJ7XTF2badEt6qBCW+502bdLm/XxWF3LhW55P5HGF0ePv6jF7M59iGjYeRYnLRKuVabrrDQEQFvo/Qa/IkqF4uGvREZmxB81giDlBQL6oYx3vTZQdotYaDeoHtDhA8eUGyFpdFQgaAVZiJF5blATw=; bm_sv=C58E68AAF778031C0B3B060EE0B1D3B3~YAAQ5wFAFy88HNSSAQAAKxHC9RlcGEEq0DWl1fM6YGmG5aK2NY9vH2A8Qb1AHvy5NWcR6ZnGAa9NKLTO1Pe0+grXbBPCo1gq6794gKP6aXlHFPXX2IAOaJVrPgd1vRejo5hzxC/fs3T7Hr4wlVuaCqwo176u2lg4JzT0JTz0CobpQKSazUDNqv5AYY3QiMChCQBjjYiUez5ufW3iHNW/iQTJSdnW/So7bjQb4n3HgRepBkGyjVaa2291BG4SwHKx~1");
            var data = JsonConvert.DeserializeObject<Dictionary<string, ReqBody>>(requestBody);
            var reqBody = data["ReqBody"];
            var jsonBody = new
            {
                Data = new
                {
                    FileIdentifier = reqBody.FileIdentifier,
                    NumberOfTransactions = reqBody.NumberOfTransactions,
                    ConsentId = reqBody.ConsentId,
                    ControSum = reqBody.ControSum,
                    SecondaryIdentification = reqBody.SecondaryIdentification,
                    DomesticPayments = new[]
                    {
                    new
                    {
                        ConsentId = reqBody.DomesticPaymentsConsentId,
                        Initiation = new
                        {
                            InstructionIdentification = reqBody.InstructionIdentification,
                            ClearingSystemIdentification = reqBody.ClearingSystemIdentification,
                            InstructedAmount = new
                            {
                                Amount = reqBody.Amount,
                                Currency = reqBody.Currency
                            },
                            DebtorAccount = new
                            {
                                Identification = reqBody.DebtorAccountIdentification,
                                Name = reqBody.DebtorAccountName,
                                SecondaryIdentification = reqBody.DebtorSecondaryIdentification,
                                Unstructured = new
                                {
                                    ContactInformation = new
                                    {
                                        MobileNumber = reqBody.MobileNumber
                                    },
                                    Identities = new { }
                                }
                            },
                            CreditorAccount = new
                            {
                                SchemeName = reqBody.CreditorSchemeName,
                                Identification = reqBody.CreditorIdentification,
                                Name = reqBody.CreditorName,
                                Unstructured = new
                                {
                                    ContactInformation = new { },
                                    Identities = new { }
                                }
                            },
                            RemittanceInformation = new
                            {
                                Unstructured = new
                                {
                                    CreditorReferenceInformation = reqBody.CreditorReferenceInformation,
                                    RemitterAccount = reqBody.RemitterAccount
                                }
                            }
                        },
                        Risk = new
                        {
                            PaymentContextCode = "BANKTRANSFER",
                            DeliveryAddress = new
                            {
                                AddressLine = new[]
                                {
                                    reqBody.AddressLine
                                }
                            }
                        }
                    }
                }
                }
            };

            var jsonString = JsonConvert.SerializeObject(jsonBody);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            var jsonResponse = JObject.Parse(result);
            var orderDetail = ReadDataFromJson2(result);
            DataTable data1 = orderDetail.Tables["data"];
            DataTable links = orderDetail.Tables["links"];
            DataTable meta = orderDetail.Tables["meta"];

            SaveReturnsDetails(data1, meta, links);
            return Content(result);
        }
        public DataSet ReadDataFromJson2(string jsonString, XmlReadMode mode = XmlReadMode.Auto)
        {
            var originaljson = jsonString;
            //// Note:Json convertor needs a json with one node as root
            jsonString = $"{{ \"rootNode\": {{{jsonString.Trim().TrimStart('{').TrimEnd('}')}}} }}";
            //// Now it is secure that we have always a Json with one node as root 
            ///
            XmlDocument xd;
            try
            {
                xd = JsonConvert.DeserializeXmlNode(jsonString);
            }
            catch (Exception eee)
            {
                jsonString = $"{{ \"rootNode\": {{{originaljson.Trim().TrimStart('{').TrimEnd('}')}}} }}" + "}";
                xd = JsonConvert.DeserializeXmlNode(jsonString);

            }

            //// DataSet is able to read from XML and return a proper DataSet
            var result = new DataSet();
            result.ReadXml(new XmlNodeReader(xd), mode);
            return result;
        }
        private void SaveReturnsDetails(DataTable data, DataTable meta, DataTable links)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    DataTable combinedTable = CombineOrderAndDataTables3(data, meta, links);
                    InsertDataIntoPayment(combinedTable, conn);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while saving order details: {ex.Message}");
            }
        }
        private DataTable CombineOrderAndDataTables3(DataTable data, DataTable meta, DataTable links)
        {
            DataTable combinedTable = new DataTable();

            // Add columns from the data DataTable
            foreach (DataColumn column in data.Columns)
            {
                combinedTable.Columns.Add(column.ColumnName, column.DataType);
            }

            // Add columns from the meta DataTable if it is not null
            if (meta != null)
            {
                foreach (DataColumn column in meta.Columns)
                {
                    if (!combinedTable.Columns.Contains(column.ColumnName))
                    {
                        combinedTable.Columns.Add(column.ColumnName, column.DataType);
                    }
                }
            }

            // Add columns from the links DataTable if it is not null
            if (links != null)
            {
                foreach (DataColumn column in links.Columns)
                {
                    if (!combinedTable.Columns.Contains(column.ColumnName))
                    {
                        combinedTable.Columns.Add(column.ColumnName, column.DataType);
                    }
                }
            }

            int maxRowCount = Math.Max(Math.Max(data.Rows.Count, meta?.Rows.Count ?? 0), links?.Rows.Count ?? 0);

            for (int i = 0; i < maxRowCount; i++)
            {
                DataRow combinedRow = combinedTable.NewRow();

                // Copy data from the data DataTable
                if (i < data.Rows.Count)
                {
                    foreach (DataColumn column in data.Columns)
                    {
                        combinedRow[column.ColumnName] = data.Rows[i][column.ColumnName];
                    }
                }

                // Copy data from the meta DataTable if it is not null
                if (meta != null && i < meta.Rows.Count)
                {
                    foreach (DataColumn column in meta.Columns)
                    {
                        if (combinedRow[column.ColumnName] == DBNull.Value)
                        {
                            combinedRow[column.ColumnName] = meta.Rows[i][column.ColumnName];
                        }
                    }
                }

                // Copy data from the links DataTable if it is not null
                if (links != null && i < links.Rows.Count)
                {
                    foreach (DataColumn column in links.Columns)
                    {
                        if (combinedRow[column.ColumnName] == DBNull.Value)
                        {
                            combinedRow[column.ColumnName] = links.Rows[i][column.ColumnName];
                        }
                    }
                }

                combinedTable.Rows.Add(combinedRow);
            }

            return combinedTable;
        }


        private void InsertDataIntoPayment(DataTable results, SqlConnection conn)
        {
            foreach (DataRow row in results.Rows)
            {
                StringBuilder columns = new StringBuilder();
                StringBuilder values = new StringBuilder();

                foreach (DataColumn column in results.Columns)
                {
                    if (columns.Length > 0)
                    {
                        columns.Append(", ");
                        values.Append(", ");
                    }

                    columns.Append(column.ColumnName);
                    values.Append("@").Append(column.ColumnName);
                }

                string query = $"INSERT INTO TYESPAY ({columns}) VALUES ({values})";

                using (SqlCommand command = new SqlCommand(query, conn))
                {
                    foreach (DataColumn column in results.Columns)
                    {
                        command.Parameters.AddWithValue($"@{column.ColumnName}", row[column.ColumnName]);
                    }

                    command.ExecuteNonQuery();
                }
            }
        }
        public class StatementRequest
        {
            public string CustomerId { get; set; }
            public string AccountNo { get; set; }
            public string PageSize { get; set; }
            public string PageNo { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
        }

        public class ConsumerContext
        {
            public string RequesterID { get; set; }
        }

        public class ServiceContext
        {
            public string ServiceName { get; set; }
            public string ReqRefNum { get; set; }
            public string ReqRefTimeStamp { get; set; }
            public string ServiceVersionNo { get; set; }
        }

        public class ReqHdr
        {
            public ConsumerContext ConsumerContext { get; set; }
            public ServiceContext ServiceContext { get; set; }
        }

        public class ReqBody
        {
            public string CustomerId { get; set; }
            public string AccountNo { get; set; }
            public string PageSize { get; set; }
            public string PageNo { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public string FileIdentifier { get; set; }
            public string NumberOfTransactions { get; set; }
            public string ConsentId { get; set; }
            public string ControSum { get; set; }
            public string SecondaryIdentification { get; set; }
            public string DomesticPaymentsConsentId { get; set; }
            public string InstructionIdentification { get; set; }
            public string ClearingSystemIdentification { get; set; }
            public string Amount { get; set; }
            public string Currency { get; set; }
            public string DebtorAccountIdentification { get; set; }
            public string DebtorAccountName { get; set; }
            public string DebtorSecondaryIdentification { get; set; }
            public string MobileNumber { get; set; }
            public string CreditorSchemeName { get; set; }
            public string CreditorIdentification { get; set; }
            public string CreditorName { get; set; }
            public string CreditorReferenceInformation { get; set; }
            public string RemitterAccount { get; set; }
            public string AddressLine { get; set; }
        }

        public class AcctStatementInquiryReq
        {
            public ReqHdr ReqHdr { get; set; }
            public ReqBody ReqBody { get; set; }
        }
        public class Data
        {
            public string FileIdentifier { get; set; }
            public string NumberOfTransactions { get; set; }
            public string ConsentId { get; set; }
            public string ControSum { get; set; }
            public string SecondaryIdentification { get; set; }
            public List<DomesticPayment> DomesticPayments { get; set; }
        }

        public class DomesticPayment
        {
            public string ConsentId { get; set; }
            public Initiation Initiation { get; set; }
            public Risk Risk { get; set; }
        }

        public class Initiation
        {
            public string InstructionIdentification { get; set; }
            public string ClearingSystemIdentification { get; set; }
            public InstructedAmount InstructedAmount { get; set; }
            public DebtorAccount DebtorAccount { get; set; }
            public CreditorAccount CreditorAccount { get; set; }
            public RemittanceInformation RemittanceInformation { get; set; }
        }

        public class InstructedAmount
        {
            public string Amount { get; set; }
            public string Currency { get; set; }
        }

        public class DebtorAccount
        {
            public string Identification { get; set; }
            public string Name { get; set; }
            public string SecondaryIdentification { get; set; }
            public Unstructured Unstructured { get; set; }
        }

        public class CreditorAccount
        {
            public string SchemeName { get; set; }
            public string Identification { get; set; }
            public string Name { get; set; }
            public Unstructured Unstructured { get; set; }
        }

        public class Unstructured
        {
            public ContactInformation ContactInformation { get; set; }
            public Dictionary<string, object> Identities { get; set; }
        }

        public class ContactInformation
        {
            public string MobileNumber { get; set; }
        }

        public class RemittanceInformation
        {
            public Unstructured Unstructured { get; set; }
        }

        public class Risk
        {
            public string PaymentContextCode { get; set; }
            public DeliveryAddress DeliveryAddress { get; set; }
        }

        public class DeliveryAddress
        {
            public List<string> AddressLine { get; set; }
        }

    }
}

