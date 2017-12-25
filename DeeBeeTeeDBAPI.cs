using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace DeeBeeTeeDB
{
    public class User
    {
        public int uid;
        public string user;
        public DateTime assign_date;
        public decimal limit;
    }
    class DBAPI
    {
        string _DataSource;
        string _UserID;
        string _Password;
        string _InitialCatalog;
        SqlConnection connection;
        string _GetUserBalanceSQL;
        string _UserSearchSQL;
        string _NewUserSQL;
        string _NewTransactionSQL;

        public DBAPI(string DataSource, string UserID, string Password, string InitialCatalog)
        {
            _DataSource = DataSource;
            _UserID = UserID;
            _Password = Password;
            _InitialCatalog = InitialCatalog;
            #region SQL
            _GetUserBalanceSQL = "SELECT (select SUM(amount) from dbo.[transactions] WHERE [from_user] = '%UserName%') - (select SUM(amount) from dbo.[transactions] WHERE [to_user] = '%UserName%') as Bal ";
            _UserSearchSQL = "SELECT uid, [user], assign_date, limit from dbo.[users] where [user] = '%UserName%'";
            _NewUserSQL = "INSERT INTO [dbo].[users] ([user] ,[assign_date],[limit]) VALUES ('%UserName%', getdate(), %Limit%) SELECT top 1 uid FROM [dbo].[users] order by assign_date DESC";
            _NewTransactionSQL = "INSERT INTO [dbo].[transactions] ([from_user] ,[amount] ,[to_user] ,[operation_date] ,[oid]) VALUES ('%FromUser%' ,%Amount% ,'%ToUser%' ,getdate() ,%OID%) SELECT top 1 tid FROM [dbo].[transactions] order by [operation_date] DESC";
            #endregion

        }
        public void Connect()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = _DataSource;
            builder.UserID = _UserID;
            builder.Password = _Password;
            builder.InitialCatalog = _InitialCatalog;
            connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
        }
        public decimal GetUserBalance(string UserName)
        {
            try
            {
                SqlCommand command = new SqlCommand(_GetUserBalanceSQL.Replace("%UserName%", UserName), connection);
                SqlDataReader reader = command.ExecuteReader();
                decimal Balance = 0 ;
                while (reader.Read())
                {
                    Balance = reader.GetDecimal(0);
                }
                
                
                reader.Close();
                return Balance;
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return 0;
            }
        }

        public User SearchUser(string UserName)
        {
            User _user = new User();
            _user.uid = 0;
            try
            {
                SqlCommand command = new SqlCommand(_UserSearchSQL.Replace("%UserName%", UserName), connection);
                SqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        _user.uid = reader.GetInt32(0);
                        _user.user = reader.GetString(1);
                        _user.assign_date = reader.GetDateTime(2);
                        _user.limit = reader.GetDecimal(3);

                    }
                }
                else
                {
                    _user.uid = 0;
                }


                reader.Close();
                return _user;
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return _user;
            }
        }

        public int NewUser(string UserName, decimal Limit)
        {
            try
            {
                string _NewUserSQLParams = _NewUserSQL.Replace("%UserName%", UserName);
                _NewUserSQLParams = _NewUserSQLParams.Replace("%Limit", Limit.ToString());
                SqlCommand command = new SqlCommand(_NewUserSQLParams, connection);
                SqlDataReader reader = command.ExecuteReader();
                int uid = 0;
                while (reader.Read())
                {
                    uid = reader.GetInt32(0);
                }

                reader.Close();
                return uid;
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return 0;
            }
        }

        public int NewTransaction(string FromUser, string ToUser, decimal Amount, int OID)
        {
            try
            {
                string _NewTransactionSQLParam = _NewTransactionSQL.Replace("%FromUser%", FromUser);
                _NewTransactionSQLParam = _NewTransactionSQLParam.Replace("%ToUser%", ToUser);
                _NewTransactionSQLParam = _NewTransactionSQLParam.Replace("%Amount%", Amount.ToString());
                _NewTransactionSQLParam = _NewTransactionSQLParam.Replace("%OID%", OID.ToString());
                SqlCommand command = new SqlCommand(_NewTransactionSQLParam, connection);
                SqlDataReader reader = command.ExecuteReader();
                int tid = 0;
                while (reader.Read())
                {
                    tid = reader.GetInt32(0);
                }

                reader.Close();
                return tid;
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return 0;
            }
        }
    }
}
