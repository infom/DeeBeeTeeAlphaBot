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
        string _GetUserDetailsSQL;

        public DBAPI(string DataSource, string UserID, string Password, string InitialCatalog)
        {
            _DataSource = DataSource;
            _UserID = UserID;
            _Password = Password;
            _InitialCatalog = InitialCatalog;
            #region SQL
            _GetUserBalanceSQL = "SELECT ISNULL((select SUM(amount) from dbo.[transactions] WHERE [from_user] = '%UserName%'),0) - ISNULL((select SUM(amount) from dbo.[transactions] WHERE [to_user] = '%UserName%'), 0) as Bal ";
            _UserSearchSQL = "SELECT uid, [user], assign_date, limit from dbo.[users] where [user] = '%UserName%'";
            _NewUserSQL = "INSERT INTO [dbo].[users] ([user] ,[assign_date],[limit]) VALUES ('%UserName%', getdate(), %Limit%) SELECT top 1 uid FROM [dbo].[users] order by assign_date DESC";
            _NewTransactionSQL = "INSERT INTO [dbo].[transactions] ([from_user] ,[amount] ,[to_user] ,[operation_date] ,[oid]) VALUES ('%FromUser%' ,%Amount% ,'%ToUser%' ,getdate() ,%OID%) SELECT top 1 tid FROM [dbo].[transactions] order by [operation_date] DESC";
            _GetUserDetailsSQL = "SELECT to_user, SUM(amount) as amount FROM (SELECT [tid], [from_user], [amount], [to_user] FROM [dbo].[transactions] union SELECT [tid], [to_user], [amount]*(-1), [from_user] FROM [dbo].[transactions] ) AS T WHERE from_user = '%UserName%' GROUP BY to_user";
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

        public void Disconnect()
        {
            connection.Close();
        }
        public decimal GetUserBalance(string UserName)
        {
            try
            {
                SqlCommand command = new SqlCommand(_GetUserBalanceSQL.Replace("%UserName%", UserName), connection);
                SqlDataReader reader = command.ExecuteReader();
                decimal Balance = 0;
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        Balance = reader.GetDecimal(0);
                    }
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

        public string GetUserDetails(string UserName)
        {
            try
            {
                SqlCommand command = new SqlCommand(_GetUserDetailsSQL.Replace("%UserName%", UserName), connection);
                SqlDataReader reader = command.ExecuteReader();
                string details = "";
                decimal amount;
                string user2;
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        amount = reader.GetDecimal(1);
                        user2 = reader.GetString(0);
                        if (amount > 0)
                        {
                            details = details + user2 + " должен " + amount.ToString() + " " + UserName + "\r\n";
                        }
                        else
                        {
                            details = details + UserName + " должен " + (amount*(-1)).ToString() + " " + user2 + "\r\n";
                        }
                        
                    }
                }

                reader.Close();
                return details;
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return e.ToString();
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
                _NewUserSQLParams = _NewUserSQLParams.Replace("%Limit%", Limit.ToString());
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
        public string Command_balance(string username)
        {
            string r;
            User su = SearchUser(username);
            if (su.uid == 0)
            {
                r = "Пользователь с именем " + username + " не найден. Для создание пользователя необходимо написать /start";
            }
            else
            {
                r = "Баланс пользователя " + username + " составляет " + GetUserBalance(username).ToString();
            }

            return r;
        }

        public string Command_details(string username)
        {
            string r;
            User su = SearchUser(username);
            if (su.uid == 0)
            {
                r = "Пользователь с именем " + username + " не найден. Для создание пользователя необходимо написать /start";
            }
            else
            {
                r = "Детали пользователя " + username + "\r\n" + GetUserDetails(username);
            }

            return r;
        }

        public string Command_hello()
        {
            string r;
            r = "Приветствую тебя дибитиант";
            return r;
        }

        public string Command_start(string username)
        {
            string r;
            User su = SearchUser(username);

            if (username.Length == 0)
            {
                r = "Невозможно добавить пользователя без имени";
                return r;
            }

            if (su.uid == 0)
            {
                int uid = NewUser(username, 1000);
                r = "Поздравляю вы подключились к системе DeeBeeTee. Ваш номер " + uid.ToString() + ". Ваш начальный лимит 1000. Удачного использования";
            }
            else
            {
                r = "Вы уже подключены к системе. \r\n" + Command_balance(username)+ ". \r\nВаш номер " + su.uid + ". \r\nДата подключения " + su.assign_date.ToString() + ". \r\nЛимит " + su.limit.ToString();
            }

            return r;
        }

        public string Command_help()
        {
            string r;
            r = "Поддерживаются команды \r\n/balance /b \r\n/details /d \r\n/hello \r\n/help \r\n/start \r\n/transaction /t";
            return r;
        }

        public string Command_transaction(string message)
        {
            string r = "";
            string m = message.Replace("/transaction","");
            m = m.Replace("/t", "");
            m = m.Replace("@DeeBeeTeeBot", "");
            m = m.Trim();
            int probel = m.IndexOf(' ');
            if (probel == -1)
            {
                r = "Команда добавления транзакции неправильная. Принимаются только команды вида @FromUser Amount @ToUser. Например '@Ivan 226 @Petr'. Не найдены пробелы";
                return r;
            }
            string from_user = m.Substring(1, probel - 1);
            m = m.Substring(probel + 1);
            m = m.Trim();
            probel = m.IndexOf(' ');
            if (probel == -1)
            {
                r = "Команда добавления транзакции неправильная. Принимаются только команды вида @FromUser Amount @ToUser. Например '@Ivan 226 @Petr'. Не найдена сумма";
                return r;
            }
            string s_amount = m.Substring(0, probel);
            decimal amount = 0;
            if (Decimal.TryParse(s_amount, out amount) == false)
            {
                r = "Команда добавления транзакции неправильная. Принимаются только команды вида @FromUser Amount @ToUser. Например '@Ivan 226 @Petr'. Сумма не преобразуется";
                return r;
            }
            m = m.Substring(probel + 2);
            m = m.Trim();
            string to_user = m;
            if (m.Length == 0)
            {
                r = "Команда добавления транзакции неправильная. Принимаются только команды вида @FromUser Amount @ToUser. Например '@Ivan 226 @Petr'. Не найден пользователь to";
                return r;
            }

            User su = SearchUser(from_user);
            if (su.uid == 0)
            {
                r = "Пользователь " + from_user + " не подключен к системею. Регистрация транзакции невозможна";
                return r;
            }

            su = SearchUser(to_user);
            if (su.uid == 0)
            {
                r = "Пользователь " + to_user + " не подключен к системею. Регистрация транзакции невозможна";
                return r;
            }

            int tid = NewTransaction(from_user, to_user, amount, 0);
            r = "Транзакция " + tid.ToString() + " успешно добавлена\r\n" + Command_balance(from_user) + "\r\n" + Command_balance(to_user);
            //r = "fromuser:'" + from_user + "' \r\namaunt:'" + s_amount + "' \r\ntouser:'" + to_user + "'";
            //User su = SearchUser(username);
            //if (su.uid == 0)
            //{
            //    int uid = NewUser(username, 1000);
            //    r = "Поздравляю вы подключились к системе DeeBeeTee. Ваш номер " + uid.ToString() + ". Ваш начальный лимит 1000. Удачного использования";
            //}
            //else
            //{
            //    r = "Вы уже подключены к системе. \r\n" + Command_balance(username) + ". \r\nВаш номер " + su.uid + ". \r\nДата подключения " + su.assign_date.ToString() + ". \r\nЛимит " + su.limit.ToString();
            //}
            return r;
        }
    }
}
