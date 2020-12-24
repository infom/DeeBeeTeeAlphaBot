﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using NLog;

namespace DeeBeeTeeDB
{
    public class User
    {
        public int uid;
        public string user;
        public DateTime assign_date;
        public decimal limit;
        public int user_id;
    }

    public class Transaction : EventArgs
    {
        public int tid;
        public User from = new User();
        public User to = new User();
        public decimal amount;
    }

    public delegate void RegTransaction(object sendr, Transaction e);

    class DBAPI
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
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
        string _GetUserJournalSQL;
        string _GetTransactionInfoSQL;
        string _DeleteTransactionSQL;
        string _UpdateChatSQL;
        string _UpdateChatUsersSQL;
        string _ChatIsolateSQL;
        string _CheckChatIsolatedSQL;
        public event RegTransaction RegTransactionEvent;
        public DBAPI(string DataSource, string UserID, string Password, string InitialCatalog)
        {
            _DataSource = DataSource;
            _UserID = UserID;
            _Password = Password;
            _InitialCatalog = InitialCatalog;
            #region SQL
            _GetUserBalanceSQL = "SELECT ISNULL((select SUM(amount) from dbo.[transactions] WHERE [from_user] = '%UserName%'),0) - ISNULL((select SUM(amount) from dbo.[transactions] WHERE [to_user] = '%UserName%'), 0) as Bal ";
            _UserSearchSQL = "SELECT uid, [user], assign_date, limit, user_id from dbo.[users] where [user] = '%UserName%'";
            _NewUserSQL = "INSERT INTO [dbo].[users] ([user] ,[assign_date],[limit], [user_id]) VALUES ('%UserName%', getdate(), %Limit%, %UserId%); insert into gnode (ID, uid, username, balance) SELECT top 1 uid, uid, '%UserName%', %Limit% FROM [dbo].[users] order by assign_date DESC; SELECT top 1 uid FROM [dbo].[users] order by assign_date DESC";
            _NewTransactionSQL = "INSERT INTO [dbo].[transactions] ([from_user] ,[amount] ,[to_user] ,[operation_date] ,[oid]) VALUES ('%FromUser%' ,%Amount% ,'%ToUser%' ,getdate() ,%OID%) SELECT top 1 tid FROM [dbo].[transactions] order by [operation_date] DESC; exec NewTransaction '%FromUser%' ,'%ToUser%', %Amount% ; exec Rebalance 1, 2";
            _GetUserDetailsSQL = "EXEC Details '%UserName%' ;";
            _GetUserJournalSQL = "select top %Count% * from( SELECT tid,  to_user, amount, operation_date from transactions where from_user = '%UserName%' union SELECT tid,  from_user, -1*amount, operation_date from transactions where to_user = '%UserName%') A order by operation_date DESC";
            _GetTransactionInfoSQL = "SELECT TOP 1 tid, from_user,  to_user, amount, operation_date from transactions where tid = %tid%";
            _DeleteTransactionSQL = "DELETE from transactions where tid = %tid% ; exec NewTransaction '%FromUser%' ,'%ToUser%', '%Amount%' ; exec Rebalance 1, 2";
            _UpdateChatSQL = "INSERT INTO [dbo].[chats] ([chat_id],[type],[title],[username]) SELECT %ChatId% ,'%ChatType%' ,'%ChatTitle%', '%ChatUsername%' WHERE NOT EXISTS (SELECT NULL FROM [dbo].[chats] WHERE [chat_id] = %ChatId%)";
            _UpdateChatUsersSQL = "INSERT INTO [dbo].[chatusers] ([chat_id],[user_id],[date_reg]) SELECT %ChatId% , %UserId%, getdate() WHERE NOT EXISTS (SELECT NULL FROM [dbo].[chatusers] WHERE [chat_id] = %ChatId% AND [user_id] = %UserId%)";
            _ChatIsolateSQL = "UPDATE [dbo].[chats] SET isolated = 1 WHERE chat_id = %ChatId%";
            _CheckChatIsolatedSQL = "SELECT isolated from [dbo].[chats] WHERE chat_id = %ChatId%";
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
                logger.Trace("GetUserBalanceSQL: " + command.CommandText);
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
                logger.Error(e.Message);
                return 0;
            }
        }

        public string GetUserDetails(string UserName)
        {
            try
            {
                SqlCommand command = new SqlCommand(_GetUserDetailsSQL.Replace("%UserName%", UserName), connection);
                logger.Trace("GetUserDetailsSQL: " + command.CommandText);
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
                        logger.Trace($"Generate dbt from: {UserName} to: {user2} amount: {amount} ");
                        if (amount > 0)
                        {
                            details = details + " @" + user2 + " должен " + amount.ToString() + " @" + UserName + "\r\n";
                        }
                        else
                        {
                            if (amount < 0)
                            { details = details + " @" + UserName + " должен " + (amount * (-1)).ToString() + " @" + user2 + "\r\n"; }
                        }
                        
                    }
                }

                reader.Close();
                return details;
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
                return e.ToString();
            }
        }

        public string GetUserJournal(string UserName, int count)
        {
            try
            {
                string SQL = _GetUserJournalSQL.Replace("%UserName%", UserName);
                SQL = SQL.Replace("%Count%", count.ToString());
                SqlCommand command = new SqlCommand(SQL, connection);
                logger.Trace("GetUserjournalSQL: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                string journal = "";
                string user2;
                decimal amount;
                int tid;
                DateTime opdate;
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        //tid,  to_user, amount, operation_date
                        tid = reader.GetInt32(0);
                        user2 = reader.GetString(1);
                        amount = reader.GetDecimal(2);
                        opdate = reader.GetDateTime(3);
                        logger.Trace($"Generate journal record from: {UserName} to: {user2} amount: {amount} tid: {tid} date: {opdate}");

                        journal = journal + $"{opdate} @{user2} {amount} /tid_{tid}\r\n";
                    }
                }

                reader.Close();
                return journal;
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
                return e.ToString();
            }
        }

        public void UpdateChat(int chat_id, string chattype, int user_id, string username, string title)
        {
            try
            {
                logger.Trace("Chat update" );
                string _ChatUpdateSQLParam = _UpdateChatSQL.Replace("%ChatId%", chat_id.ToString());
                _ChatUpdateSQLParam = _ChatUpdateSQLParam.Replace("%ChatType%", chattype);
                _ChatUpdateSQLParam = _ChatUpdateSQLParam.Replace("%ChatTitle%", title);
                _ChatUpdateSQLParam = _ChatUpdateSQLParam.Replace("%ChatUsername%", username);
                SqlCommand command = new SqlCommand(_ChatUpdateSQLParam, connection);
                logger.Trace("ChatUpdateSQL: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                reader.Close();
             }
            catch (SqlException e)
            {
                logger.Error(e.Message);
             }

            try
            {
                logger.Trace("Chat users update");
                string _ChatUsersUpdateSQLParam = _UpdateChatUsersSQL.Replace("%ChatId%", chat_id.ToString());
                _ChatUsersUpdateSQLParam = _ChatUsersUpdateSQLParam.Replace("%UserId%", user_id.ToString());
                SqlCommand command = new SqlCommand(_ChatUsersUpdateSQLParam, connection);
                logger.Trace("ChatUsersUpdateSQL: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                reader.Close();
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
            }
        }

        public bool CheckIsolated(int chat_id)
        {
            try
            {
                logger.Trace("Check chat isolated");
                bool BCheck;
                string __CheckChatIsolatedSQLParam = _CheckChatIsolatedSQL.Replace("%ChatId%", chat_id.ToString());
                SqlCommand command = new SqlCommand(__CheckChatIsolatedSQLParam, connection);
                logger.Trace("CheckChatIsolatedSQL: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    reader.Read();
                    BCheck = reader.GetBoolean(0);
                }
                else
                {
                    BCheck = false;
                }


                reader.Close();
                return BCheck;
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
                return false;
            }

        }

        public User SearchUser(string UserName)
        {
            User _user = new User();
            _user.uid = 0;
            try
            {
                SqlCommand command = new SqlCommand(_UserSearchSQL.Replace("%UserName%", UserName), connection);
                logger.Trace("UserSearchSQL: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        _user.uid = reader.GetInt32(0);
                        _user.user = reader.GetString(1);
                        _user.assign_date = reader.GetDateTime(2);
                        _user.limit = reader.GetDecimal(3);
                        if (! reader.IsDBNull(4))
                            { _user.user_id = reader.GetInt32(4); }
                        

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
                logger.Error(e.Message);
                return _user;
            }
        }

        public int NewUser(int user_id, string UserName, decimal Limit)
        {
            try
            {
                string _NewUserSQLParams = _NewUserSQL.Replace("%UserName%", UserName);
                _NewUserSQLParams = _NewUserSQLParams.Replace("%Limit%", Limit.ToString());
                _NewUserSQLParams = _NewUserSQLParams.Replace("%UserId%", user_id.ToString());
                SqlCommand command = new SqlCommand(_NewUserSQLParams, connection);
                logger.Trace("NewUserSQLParams: " + command.CommandText);
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
                logger.Error(e.Message);
                return 0;
            }
        }

        public int ChatIsolate(int chat_id)
        {
            try
            {
                string _ChatIsolateSQLParams = _ChatIsolateSQL.Replace("%ChatId%", chat_id.ToString());
                SqlCommand command = new SqlCommand(_ChatIsolateSQLParams, connection);
                logger.Trace("ChatIsolateSQLParams: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                return 1;
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
                return 0;
            }
        }
        

        public int NewTransaction(string FromUser, string ToUser, decimal Amount, string Description = "")
        {
            try
            {
                string _NewTransactionSQLParam = _NewTransactionSQL.Replace("%FromUser%", FromUser);
                _NewTransactionSQLParam = _NewTransactionSQLParam.Replace("%ToUser%", ToUser);
                _NewTransactionSQLParam = _NewTransactionSQLParam.Replace("%Amount%", Amount.ToString());
                _NewTransactionSQLParam = _NewTransactionSQLParam.Replace("%OID%", "0");
                SqlCommand command = new SqlCommand(_NewTransactionSQLParam, connection);
                logger.Trace("NewTransactionSQLParam: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                int tid = 0;
                while (reader.Read())
                {
                    tid = reader.GetInt32(0);
                }

                reader.Close();
                User su = SearchUser(ToUser);
                Transaction tran = new Transaction();
                tran.amount = Amount;
                tran.from.user = FromUser;
                tran.to.user = ToUser;
                tran.to.user_id = su.user_id;
                RegTransactionEvent(this, tran);



                return tid;
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
                return 0;
            }
        }
        public string Command_balance(string username, string cparams = "")
        {
            string r;
            logger.Debug($"Поиск пользователя '{username}'");
            User su = SearchUser(username);
            if (su.uid == 0)
            {
                r = "Пользователь с именем " + username + " не найден. Для создание пользователя необходимо написать /start";
            }
            else
            {
                r = "Баланс пользователя @" + username + " составляет " + GetUserBalance(username).ToString();
            }
            logger.Info("Возврат результата команды balance с сообщением " + r);
            return r;
        }

        public string Command_a_balance(string message)
        {
            string r;
            string username = message.Trim();
            username = username.Replace("/a_balance", "");
            username = username.Trim();
            username = username.Substring(1, username.Length - 1);
            logger.Debug($"Поиск пользователя '{username}'");
            User su = SearchUser(username);
            if (su.uid == 0)
            {
                r = "Пользователь с именем " + username + " не найден. Для создание пользователя необходимо написать /start";
            }
            else
            {
                r = "Баланс пользователя @" + username + " составляет " + GetUserBalance(username).ToString();
            }
            logger.Info("Возврат результата команды balance с сообщением " + r);
            return r;
        }

        public string Command_details(string username, string cparams)
        {
            string r;
            logger.Debug($"Поиск пользователя '{username}'");
            User su = SearchUser(username);
            if (su.uid == 0)
            {
                r = "Пользователь с именем @" + username + " не найден. Для создание пользователя необходимо написать /start";
            }
            else
            {
                r = "Детали пользователя @" + username + ". Баланс: " + GetUserBalance(username).ToString() + "\r\n" + GetUserDetails(username);
            }

            logger.Info("Возврат результата команды details с сообщением" + r);
            return r;
        }

        public string Command_tid(string command, string cparams)
        {
            string r = "";
            string tidstr;
            int tid;
            logger.Debug($"Получение tid из '{command}'");

            tidstr = command.Replace("/tid_", "").Trim();

            if (int.TryParse(tidstr, out tid) == false)
            {
                r = "Команда управления транзакцией неправильная. Принимаются только команды вида /tid_XXX. Где XXX - это номер транзакции";

                return r;
            }

            if (cparams.Trim() == "")
            {
                logger.Debug($"Запрос информации по транзакции '{tid}'");
                r = "Информация о транзакции \r\n" + GetTransactionInfo(tid)+ "\r\n Для удаления транзакции введите /tid_" + tidstr+" delete";
            }
            else if (cparams.Trim() == "delete")
            {
                logger.Debug($"Удаление транзакции '{tid}'");
                r = DeleteTransaction(tid);
            }

            logger.Info("Возврат результата команды tid с сообщением" + r);
            return r;
        }

        private string DeleteTransaction(int tid)
        {
            try
            {
                string SQL = _GetTransactionInfoSQL.Replace("%tid%", tid.ToString());
                SqlCommand commandI = new SqlCommand(SQL, connection);
                logger.Trace("_GetTransactionInfoSQL: " + commandI.CommandText);
                SqlDataReader readerI = commandI.ExecuteReader();
                string user_from = "";
                string user_to = "";
                decimal amount = 0;
                if (readerI.HasRows)
                {
                    while (readerI.Read())
                    {
                        //tid, from_user,  to_user, amount, operation_date
                        user_from = readerI.GetString(1);
                        user_to = readerI.GetString(2);
                        amount = readerI.GetDecimal(3);
                        logger.Trace($"Get transaction info from: {user_from} to: {user_to} tid: {tid} amount:{amount}");
                    }
                }

                readerI.Close();

                SQL = _DeleteTransactionSQL.Replace("%tid%", tid.ToString());
                SQL = SQL.Replace("%ToUser%", user_from);
                SQL = SQL.Replace("%FromUser%", user_to);
                SQL = SQL.Replace("%Amount%", amount.ToString().Replace(",","."));
                SqlCommand commandD = new SqlCommand(SQL, connection);
                logger.Trace("_DeleteTransactionSQL: " + commandD.CommandText);
                SqlDataReader readerD = commandD.ExecuteReader();
                readerD.Close();

                string r = "Транзакция "+tid.ToString()+ " успешно удалена!\r\n"+ Command_balance(user_from)+"\r\n"+ Command_balance(user_to);

                return r;
                ;
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
                return e.ToString();
            }
        }

        private string GetTransactionInfo(int tid)
        {
            try
            {
                string SQL = _GetTransactionInfoSQL.Replace("%tid%", tid.ToString());
                SqlCommand command = new SqlCommand(SQL, connection);
                logger.Trace("_GetTransactionInfoSQL: " + command.CommandText);
                SqlDataReader reader = command.ExecuteReader();
                string tinfo = "";
                string user_from;
                string user_to;
                decimal amount;
                DateTime opdate;
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        //tid, from_user,  to_user, amount, operation_date
                        user_from = reader.GetString(1);
                        user_to = reader.GetString(2);
                        amount = reader.GetDecimal(3);
                        opdate = reader.GetDateTime(4);

                        logger.Trace($"Generate transaction info record from: {user_from} to: {user_to} amount: {amount} tid: {tid} date: {opdate}");

                        tinfo = $"tid: {tid}\r\n От кого: @{user_from}\r\n Кому: @{user_to}\r\n Сумма: {amount}\r\n Дата: {opdate}";

                    }
                }

                reader.Close();
                return tinfo;
            }
            catch (SqlException e)
            {
                logger.Error(e.Message);
                return e.ToString();
            }
        }

        public string Command_a_details(string message)
        {
            string r;
            string username = message.Trim();
            username = username.Replace("/a_details", "");
            username = username.Trim();
            username = username.Substring(1, username.Length - 1);
            logger.Debug($"Поиск пользователя '{username}'");
            User su = SearchUser(username);
            if (su.uid == 0)
            {
                r = "Пользователь с именем @" + username + " не найден. Для создание пользователя необходимо написать /start";
            }
            else
            {
                r = "Детали пользователя @" + username + ". Баланс: " + GetUserBalance(username).ToString() + "\r\n" + GetUserDetails(username);
            }

            logger.Info("Возврат результата команды details с сообщением" + r);
            return r;
        }

        public string Command_hello()
        {
            string r;
            r = "Приветствую тебя дибитиант";
            logger.Info("Возврат результата команды hello с сообщением " + r);
            return r;
        }

        public string Command_start(string username, int user_id)
        {
            string r;
            logger.Debug($"Поиск пользователя '{username}'");

            if (String.IsNullOrEmpty(username))
            {
                r = "Невозможно добавить пользователя без имени. Заполните username!";
                return r;
            }

            if (username.Length == 0)
            {
                r = "Невозможно добавить пользователя без имени. Заполните username!";
                return r;
            }

            User su = SearchUser(username);


            if (su.uid == 0)
            {
                logger.Debug($"Добавление пользователя '{username}' с лимитом 1000");
                int uid = NewUser(user_id, username, 1000);
                r = "Поздравляю вы подключились к системе DeeBeeTee. Ваш номер " + uid.ToString() + ". Удачного использования";
            }
            else
            {
                r = "Вы уже подключены к системе. \r\n" + Command_balance(username)+ ". \r\nВаш номер " + su.uid + ". \r\nДата подключения " + su.assign_date.ToString() + ". \r\nЛимит " + su.limit.ToString();
            }
            logger.Info("Возврат результата команды start с сообщением " + r);
            return r;
        }

        public string Command_help(string cparams)
        {
            string r;
            r = "Поддерживаются команды \r\n/balance /b \r\n/details /d \r\n/hello \r\n/help \r\n/start \r\n/transaction /t";
            logger.Info("Возврат результата команды help с сообщением " + r);
            return r;
        }

        public string Command_journal(string username, string cparams)
        {
            int cnt;
            string r;
            if (int.TryParse(cparams, out cnt) == false)
            {
                r = "Команда журнала транзакций неправильная. Принимаются только команды вида /j 100. Где 100 - это количество последних записей";
                return r;
            }

            r = "Журнал операций пользователя @" + username + ". Последние " +cnt.ToString() +" записей \r\n" + GetUserJournal(username, cnt);

            logger.Info("Возврат результата команды journal с сообщением" + r);
            return r;
        }

        
        public string Command_isolate_request(string username, int chatid, string cparams)
        {
            string r;

            r = "Внимание вы пытаетесь изолировать данный чат. \r\nДанная процедура приведет к тому что все транзакции, регистрируемые в нем, будут видны только в этом чате. Нигде в другом месте они отображаться не будут. Так же не будет выполняться балансировка долгов между всему участниками системы, а только внутри чата!" 
                + ". Процедура необратима. Если вы хотите её выполнить, то кликнете по команде /isolate_" + chatid.ToString() + " . Хорошо подумайте перед выполнением";

            logger.Info("Возврат результата команды isolate с сообщением" + r);
            return r;
        }

        public string Command_private(string username, string cparams)
        {
            string r;

            r = "Здесь будет команда рассылки";

            logger.Info("Возврат результата команды private с сообщением" + r);
            return r;
        }
        
        public string Command_isolate(string command, int chatid, string cparams)
        {
            string r = "";
            string chatstr;
            int chatid_fromcommand;
            logger.Debug($"Получение chatid из '{command}'");

            chatstr = command.Replace("/isolate_", "").Trim();

            if (int.TryParse(chatstr, out chatid_fromcommand) == false)
            {
                r = "Команда изоляции чата неправильная. Принимаются только команды вида /isolate_XXX. Где XXX - это номер чата";
                return r;
            }

            if (chatid != chatid_fromcommand)
            {
                r = "Команда изоляции чата неправильная. Принимаются только команды вида /isolate_XXX. Где XXX - это номер чата. Команды принимаются только из этого же чата";
                return r;
            }

            logger.Debug($"Попытка изоляции чата'{chatid}'");

            int uid = ChatIsolate(chatid);

            r = "Поздравляем ваш чат №" + chatid.ToString() + " изолирован. Теперь никто не узнает о транзакциях и долгах, регистрируемых здесь вне этого чата! Всем пользователям этого чата необходимо выполнить команду /start в этом чате заново !";


            logger.Info("Возврат результата команды isolate с сообщением " + r);
            return r;

        }

        public string Command_transaction(string username, string message)
        {
            const string _tHelp = "\r\nПринимаются только команды вида @FromUser Amount @ToUser(s). Например '@Ivan 226 @Petr'. @FromUser можно опустить, тогда он будет соответствовать текущему пользователю. Amount может быть простым арифметическим выражением (без пробелов), например 1000-120+58, 242*3, (756+42)*2";

            string r = "";
            string m = message.Replace("/transaction","");
            string[] words = message.Trim().Split(' ');
            string desc = "";
            string from_user;
            m = m.Replace("/t", "");
            m = m.Replace("@DeeBeeTeeBot", "");
            m = m.Replace("@DeeBeeTeeTestBot", "");
            m = m.Trim();
            logger.Debug($"Начала парсинга транзакции в строке '{m}'");
            int probel = m.IndexOf(' ');
            if (probel == -1)
            {
                r = "Команда добавления транзакции неправильная (не найдены пробелы). " + _tHelp;
                return r;
            }
            string after_t = m.Substring(0, probel);
            logger.Debug($"Сразу после /t '{after_t}'");

            if (!after_t.Trim().StartsWith("@"))
            {
                logger.Debug("Видимо пользователь не указан, берем отправителя");
                from_user = username;
            }
            else
            {
                from_user = m.Substring(1, probel - 1);
                logger.Debug($"Пользователь from '{from_user}'");
                m = m.Substring(probel + 1);
                m = m.Trim();
                probel = m.IndexOf(' ');
                if (probel == -1)
                {
                    r = "Команда добавления транзакции неправильная (не найдена сумма). " + _tHelp;
                    return r;
                }
            }

            
            string s_amount = m.Substring(0, probel);
            decimal amount = 0;
            bool computed = false;
            
            try 
            {
                amount = Convert.ToDecimal(new DataTable().Compute(s_amount, null));
                amount = Decimal.Ceiling(amount);
                computed = true;
            }
            catch (Exception e) 
            {
                logger.Debug(e.ToString());
            }

            logger.Debug($"Сумма '{s_amount} ({(computed ? amount.ToString() : "не вычислено")})'");
            
            if (!computed)
            {
                r = "Команда добавления транзакции неправильная (не вычислена сумма). " + _tHelp;
                return r;
            }

            m = m.Substring(probel + 2);
            m = m.Trim();
            List<string> ToUsers = new List<string>();
            bool FromUserInMulti = false;
            bool SimpleTransaction = true;
            int AtPos = m.IndexOf("@", 2, m.Length-2, StringComparison.CurrentCulture);
            if (AtPos > 0)
            {
                string ToUser;
                SimpleTransaction = false;
                m = "@" + m + " ";
                logger.Debug("Обнаружена мультитранзакция, поиск пользователей в подстроке '" + m);
                //logger.Trace("Search in '" + m + "'");
                for (int p = 0; p < m.Length; p++)
                {
                    //logger.Trace("search " + p);
                    if (m[p] == '@')
                    {
                        AtPos = p;
                        //logger.Trace("at found  " + p);
                    };

                    if ((m[p] == ' ')&(AtPos >= 0))
                    {
                        //logger.Trace("space found  " + p);
                        ToUser = m.Substring(AtPos + 1, p - 1 - AtPos);
                        logger.Trace("add user  '" + ToUser + "'");
                        ToUsers.Add(ToUser);
                        FromUserInMulti = FromUserInMulti || (ToUser.ToLower() == from_user.ToLower());
                        AtPos = -1;

                        if (p < m.Length - 1)
                            if (m[p + 1] != '@')
                            {
                                desc = m.Substring(p + 1, m.Length - p - 1);
                                desc = desc.Trim();
                                logger.Trace("find description '" + desc + "'");
                                continue;
                            };
                    };



                };

                logger.Trace("Количество пользователей to в мультитранзакции " + ToUsers.Count + ", сам пользователь " + FromUserInMulti);
            };
            if (SimpleTransaction)
            {
                string to_user = m;
                desc = "";
                int SpacePos = m.IndexOf(" ", 2, m.Length - 2, StringComparison.CurrentCulture);
                if (SpacePos > 0)
                {
                    to_user = m.Substring(0, SpacePos);
                    desc = m.Substring(SpacePos + 1, m.Length - SpacePos - 1);
                }
                logger.Debug($"Пользователь to '{to_user}'");
                logger.Debug($"Описание '{desc}'");
                if (m.Length == 0)
                {
                    r = "Команда добавления транзакции неправильная (не найден пользователь to). " + _tHelp;
                    return r;
                }
                logger.Debug($"Поиск пользователя '{from_user}'");
                User su = SearchUser(from_user);
                if (su.uid == 0)
                {
                    r = "Пользователь @" + from_user + " не подключен к системе. Регистрация транзакции невозможна";
                    return r;
                }
                logger.Debug($"Поиск пользователя '{to_user}'");
                su = SearchUser(to_user);
                if (su.uid == 0)
                {
                    r = "Пользователь @" + to_user + " не подключен к системе. Регистрация транзакции невозможна";
                    return r;
                }
                logger.Debug($"Выполнение в SQL транзакции from:{from_user} amount:{amount} to:{to_user}");
                int tid = NewTransaction(from_user, to_user, amount, desc);
                r = "Транзакция на сумму " + amount + " с номером " + tid.ToString() + " успешно добавлена\r\n" + Command_balance(from_user) + "\r\n" + Command_balance(to_user);
            }
            else
            {
                logger.Debug($"Поиск пользователя '{from_user}'");
                User su = SearchUser(from_user);
                if (su.uid == 0)
                {
                    r = "Пользователь @" + from_user + " не подключен к системе. Регистрация мультитранзакции невозможна";
                    return r;
                }

                for (int u = 0; u < ToUsers.Count; u++)
                {
                    string to_user = ToUsers[u];
                    logger.Debug($"Пользователь номер '{u}' to '{to_user}'");
                    logger.Debug($"Поиск пользователя '{to_user}'");
                    su = SearchUser(to_user);
                    if (su.uid == 0)
                    {
                        r = "Пользователь @" + to_user + " не подключен к системе. Регистрация мультитранзакции невозможна";
                        return r;
                    }
                };

                decimal c_amount = Math.Floor(amount / (ToUsers.Count));
                string b = "";
                decimal last_amount = amount - c_amount * (ToUsers.Count - 1);
                r = "Обнаружена команда мультитранзакции, будет добавлено несколько транзакций с делением суммы на целые части\r\n";
                for (int u = 0; u < ToUsers.Count; u++)
                {
                    string to_user = ToUsers[u];
                    int tid;
                    if (from_user.ToLower() == to_user.ToLower()) { continue; };
                    amount = c_amount;
                    if (u < ToUsers.Count - 1)
                    {
                        logger.Debug($"Выполнение в SQL транзакции from:{from_user} amount:{c_amount} to:{to_user}");
                        tid =  NewTransaction(from_user, to_user, c_amount, desc);
                    }
                    else
                    {
                        logger.Debug($"Выполнение в SQL транзакции from:{from_user} amount:{last_amount} to:{to_user}");
                        tid =  NewTransaction(from_user, to_user, last_amount, desc);

                    };
                    r = r + "Транзакция " + tid.ToString() + " успешно добавлена\r\n" ;
                    b = b + Command_balance(to_user) + "\r\n";
                };
                r = r + Command_balance(from_user) + "\r\n" + b;


            };
            logger.Info("Возврат результата команды transaction с сообщением " + r);
            return r;
        }
    }
}
