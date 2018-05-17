using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram;
using Telegram.Request;
using DeeBeeTeeDB;
using NLog;

namespace DeeBeeTeeAlphaBot
{
    class Bot
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void Start()
        {
            logger.Info("Запуск бота");
            TelegramRequest Tr = new TelegramRequest(MainSettings.Default.Token, MainSettings.Default.API_URL);
            Tr.MessageText += Tr_MessageText;
            Tr.MessageSticker += Tr_MessageSticker;
            Tr.MessagePhoto += Tr_MessagePhoto;
            Tr.MessageVideo += Tr_MessageVideo;
            Tr.MessageDocument += Tr_MessageDocument;
            Tr.MessageLocation += Tr_MessageLocation;
            Tr.MessageContact += Tr_MessageContact;
            Tr.MessageVoice += Tr_MessageVoice;

            DBAPI Db = new DBAPI(MainSettings.Default.DB_DataSource, MainSettings.Default.DB_UserID, MainSettings.Default.DB_Password, MainSettings.Default.DB_InitialCatalog);
            Db.RegTransactionEvent += RegisterTransaction;


            logger.Info("Подключение к БД");
            Db.Connect();

            logger.Info("Запуск пуллинга telegram API");
            Tr.GetUpdates();

            //Console.ReadLine();

        }

        public void Stop()
        {

        }

        private void RegisterTransaction(object sendr, Transaction e)
        {

            logger.Info("Отправка сообщения пользователю о новой транзакции в личный чат");
            try
            {
                Method m = new Method(MainSettings.Default.Token, MainSettings.Default.API_URL);
                m.SendMessage($"Пользователь @{e.from.user} зарегистрировал с вами новую транзакцию на сумму {e.amount}", e.to.user_id);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        private void Tr_MessageText(object sendr, MessageText e)
        {
            logger.Debug($"New message: message_id:{e.message_id} user_id:{e.from.id} chat_id:{e.chat.id} username:{e.from.username} name: {e.from.first_name} {e.from.last_name} date: {e.date} message:'{e.text}'");
            Method m = new Method(MainSettings.Default.Token, MainSettings.Default.API_URL);
            DBAPI d = new DBAPI(MainSettings.Default.DB_DataSource, MainSettings.Default.DB_UserID, MainSettings.Default.DB_Password, MainSettings.Default.DB_InitialCatalog);
            d.RegTransactionEvent += RegisterTransaction;
            d.Connect();
            string answer;
            string message = e.text.Replace("@DeeBeeTeeBot", "");
            string command_params = "";
            int space = message.IndexOf(" ");
            string command;
            if (space == -1)
            {
                command = message;
            }
            else
            {
                command = message.Substring(0, space);
                command_params = message.Substring(space, message.Length - space);
            }

            logger.Info("Получение команды " + command);
            switch (command)
            {
                case "/balance":
                    answer = d.Command_balance(e.from.username, command_params);
                    break;
                case "/b":
                    answer = d.Command_balance(e.from.username, command_params);
                    break;
                case "/details":
                    answer = d.Command_details(e.from.username, command_params);
                    break;
                case "/d":
                    answer = d.Command_details(e.from.username, command_params);
                    break;
                case "/hello":
                    answer = d.Command_hello();
                    break;
                case "/help":
                    answer = d.Command_help(command_params);
                    break;
                case "/start":
                    answer = d.Command_start(e.from.username, e.from.id);
                    break;
                case "/t":
                    answer = d.Command_transaction(message);
                    break;
                case "/transaction":
                    answer = d.Command_transaction(message);
                    break;
                case "/terminate7165":
                    Environment.Exit(0);
                    answer = "exiting...";
                    break;
                default:
                    answer = $"Извините я команду '{command}' не поддерживаю. Поддерживаемые команды можно посмотреть /help";
                    break;
            }

            m.SendMessage(answer, e.chat.id);
            d.UpdateChat(e.chat.id, e.chat.type, e.from.id, e.from.username, e.chat.title);
            // Сообщение мне!
            m.SendMessage("New " + e.chat.type + " message from @" + e.from.username + ": " + message, 2730927);
            d.Disconnect();

        }
        private void Tr_MessageSticker(object sendr, MessageSticker e)
        {
            Console.WriteLine("ID сообщения:{0}\nID отправителя:{1}\nНик отправителя:{2}\nИмя:{3} Фамилия:{4}\nДата:{5}\nШирина стикера:{6}  Высота стикера:{7}\nСмайлик:{8}\nЭскиз[id:{9} Размер:{10} Ширина:{11} Высота:{12}]\nID файла:{13}\nРазмер файла:{14}",
                e.message_id, e.from.id, e.from.username, e.from.first_name, e.from.last_name, e.date, e.width, e.height, e.emoji, e.thumb.file_id, e.thumb.file_size, e.thumb.width, e.thumb.height, e.file_id, e.file_size);
            Console.WriteLine();
        }
        private void Tr_MessagePhoto(object sendr, MessagePhoto e)
        {
            Console.WriteLine("ID сообщения:{0}\nID отправителя:{1}\nНик отправителя:{2}\nИмя:{3} Фамилия:{4}\nДата:{5}\nКомментарий к фотографии:{6}",
                e.message_id, e.from.id, e.from.username, e.from.first_name, e.from.last_name, e.date, e.caption);
            for (int i = 0; i < e.photo.Count; i++)
            {
                Console.WriteLine("Фото №{0}", i + 1);
                Console.WriteLine("ID Файла:{0}\nРазмер файла:{1}байт\nШирина:{2} Высота:{3}\n",
                    e.photo[i].file_id, e.photo[i].file_size, e.photo[i].width, e.photo[i].height);
            }
            Method m = new Method(MainSettings.Default.Token, MainSettings.Default.API_URL);
            m.SendMessage("Вы нам прислалим фотографию", e.chat.id);
            m.SendPhoto_link(e.chat.id, e.photo[e.photo.Count - 1].file_id, e.caption);
        }
        private void Tr_MessageVideo(object sendr, MessageVideo e)
        {
            Console.WriteLine("ID сообщения:{0}\nID отправителя:{1}\nНик отправителя:{2}\nИмя:{3} Фамилия:{4}\nДата:{5}\nID видео:{6}\nШирина:{7} Высота:{8}\nПродолжительность:{9}секунд\nРазмер:{10}байт\nЭскриз[ID видео:{11} Ширина:{12} Высота:{13} Размер:{14}]\nMime_type:{15}",
                 e.message_id, e.from.id, e.from.username, e.from.first_name, e.from.last_name, e.date, e.file_id, e.width, e.height, e.duration, e.file_size, e.thumb.file_id, e.thumb.width, e.thumb.height, e.thumb.file_size, e.mime_type);
            Console.WriteLine();
        }
        private void Tr_MessageDocument(object sendr, MessageDocument e)
        {
            Console.WriteLine("ID сообщения:{0}\nID отправителя:{1}\nНик отправителя:{2}\nИмя:{3} Фамилия:{4}\nДата:{5}\nНазвание документа:{6}\nMime type:{7}\nID Документа:{8}\nРазмер документа:{9}байт",
                e.message_id, e.from.id, e.from.username, e.from.first_name, e.from.last_name, e.date, e.file_name, e.mime_type, e.file_id, e.file_size);
            Console.WriteLine("Эскиз[ID Документа:{0} Размер файла:{1}байт Ширина:{2} Высота:{3}]", e.thumb.file_id, e.thumb.file_size, e.thumb.width, e.thumb.height);
            Console.WriteLine();
        }
        private void Tr_MessageLocation(object sendr, MessageLocation e)
        {
            Console.WriteLine("ID сообщения:{0}\nID отправителя:{1}\nНик отправителя:{2}\nИмя:{3} Фамилия:{4}\nДата:{5}\nШирина:{6} Долгота:{7}",
                e.message_id, e.from.id, e.from.username, e.from.first_name, e.from.last_name, e.date, e.latitude, e.longitude);
            Console.WriteLine();
        }
        private void Tr_MessageContact(object sendr, MessageContact e)
        {
            Console.WriteLine("ID сообщения:{0}\nID отправителя:{1}\nНик отправителя:{2}\nИмя:{3} Фамилия:{4}\nДата:{5}\nНомер телефона:{6}\nИмя:{7} Фамилия:{8}\nID Пользователя(Если зарегистриован):{9}",
                 e.message_id, e.from.id, e.from.username, e.from.first_name, e.from.last_name, e.date, e.phone_number, e.first_name, e.last_name, e.user_id);
            Console.WriteLine();
        }
        private void Tr_MessageVoice(object sendr, MessageVoice e)
        {
            Console.WriteLine("ID сообщения:{0}\nID отправителя:{1}\nНик отправителя:{2}\nИмя:{3} Фамилия:{4}\nДата:{5}\nID Аудиозаписи:{6}\nДлинна записи:{7}\nMime_type:{8}\nРазмер записи:{9}",
                  e.message_id, e.from.id, e.from.username, e.from.first_name, e.from.last_name, (new DateTime(1970, 1, 1, 0, 0, 0, 0)).AddSeconds(e.date), e.file_id, e.duration, e.mime_type, e.file_size);
            Console.WriteLine();
        }
    }
}
