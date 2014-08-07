using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Configuration;
using System.IO;
using System.Data.SqlClient;

namespace HemoBackupService
{
	partial class HBackup : ServiceBase
	{
		private System.Timers.Timer Tmr1=null;
		private System.Timers.Timer tmrFirtTime = null;
		private static string m_logDir = String.Empty;
		private static string m_backupDir = String.Empty;
		public HBackup()
		{
			InitializeComponent();
		}

		public  void HemoBackupService()
		{
			Output("Starting SQL Database Backup...\n");
			bool doDateStamp = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["DateStampBackupFiles"].ToString());
			bool success = DoBackups(m_backupDir, doDateStamp);

			if (success)
			{
				Output("Backup of SQL Server Databases ran with no errors.\n\n");

				if (Boolean.Parse(ConfigurationManager.AppSettings["DeleteOldBackups"]) == true)
				{
					DeleteOldBackups();
				}
			}
			

		}
		/// <summary>
		/// Prints message to the console window and logs the same information in the log file
		/// </summary>
		/// <param name="message"></param>
		private static void Output(string message)
		{
			Console.WriteLine(LogWriter.GetLogFileEntryDateString(DateTime.Now) + " " + message);
			LogWriter.WriteLogToTextFile(m_logDir, message);
		}

		private static void Initialise()
		{
			m_backupDir = ConfigurationManager.AppSettings["SQLBackupLocation"].ToString();

			m_logDir = ConfigurationManager.AppSettings["LoggingPath"].ToString();

			if (!Directory.Exists(m_backupDir))
			{
				Directory.CreateDirectory(m_backupDir);
			}

			if (!Directory.Exists(m_logDir))
			{
				Directory.CreateDirectory(m_logDir);
			}
		}

		/// <summary>
		/// Backs up non system SQL Server databases to the configured directory.
		/// </summary>
		/// <param name="backupDir"></param>
		/// <param name="dateStamp"></param>
		private static bool DoBackups(string backupDir, bool dateStamp)
		{
			bool allBackupsSuccessful = false;

			StringBuilder sb = new StringBuilder();

			// Build the TSQL statement to run against your databases.
			// SQL is coded inline for portability, and to allow the dynamic
			// appending of datestrings to file names where configured.

			sb.AppendLine(@"DECLARE @name VARCHAR(50) -- database name  ");
			sb.AppendLine(@"DECLARE @path VARCHAR(256) -- path for backup files  ");
			sb.AppendLine(@"DECLARE @fileName VARCHAR(256) -- filename for backup ");
			sb.AppendLine(@"DECLARE @fileDate VARCHAR(20) -- used for file name ");
			sb.AppendLine(@"SET @name= " + ConfigurationManager.AppSettings["DbName"].ToString());
			sb.AppendLine(@"SET @path = '" + backupDir + "'  ");
			sb.AppendLine(@"SELECT @fileDate = CONVERT(VARCHAR(20),GETDATE(),112) ");
			sb.AppendLine(@"DECLARE db_cursor CURSOR FOR  ");
			sb.AppendLine(@"SELECT name ");
			sb.AppendLine(@"FROM master.dbo.sysdatabases ");
			sb.AppendLine(@"WHERE name IN (@name)  ");
			sb.AppendLine(@"OPEN db_cursor   ");
			sb.AppendLine(@"FETCH NEXT FROM db_cursor INTO @name   ");
			sb.AppendLine(@"WHILE @@FETCH_STATUS = 0   ");
			sb.AppendLine(@"BEGIN   ");

			if (dateStamp)
			{
				sb.AppendLine(@"SET @fileName = @path + @name + '_' + @fileDate + '.bak'  ");
			}
			else
			{
				sb.AppendLine(@"SET @fileName = @path + @name + '.bak'  ");
			}
			sb.AppendLine(@"BACKUP DATABASE @name TO DISK = @fileName  ");
			sb.AppendLine(@"FETCH NEXT FROM db_cursor INTO @name   ");
			sb.AppendLine(@"END   ");
			sb.AppendLine(@"CLOSE db_cursor   ");
			sb.AppendLine(@"DEALLOCATE db_cursor; ");

			string connectionStr = ConfigurationManager.AppSettings["DBConnectionString"].ToString();

			SqlConnection conn = new SqlConnection(connectionStr);

			SqlCommand command = new SqlCommand(sb.ToString(), conn);

			try
			{
				conn.Open();
				command.CommandTimeout = 600;
				command.ExecuteNonQuery();

				allBackupsSuccessful = true;
			}
			catch (Exception ex)
			{
				Output("An error occurred while running the backup query: " + ex);
			}
			finally
			{
				try
				{
					conn.Close();
				}
				catch (Exception ex)
				{
					Output("An error occurred while trying to close the database connection: " + ex);
				}
			}

			return allBackupsSuccessful;
		}

		/// <summary>
		/// Delete back up files in configured directory older than configured days.
		/// </summary>
		private static void DeleteOldBackups()
		{
			String[] fileInfoArr = Directory.GetFiles(ConfigurationManager.AppSettings["SQLBackupLocation"].ToString());

			for (int i = 0; i < fileInfoArr.Length; i++)
			{
				bool fileIsOldBackUp = CheckIfFileIsOldBackup(fileInfoArr[i]);

				if (fileIsOldBackUp)
				{
					File.Delete(fileInfoArr[i]);

					Output("Deleting old backup file: " + fileInfoArr[i]);
				}
			}
		}

		/// <summary>
		/// Parses file name and returns true if file is older than configured days.
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		private static bool CheckIfFileIsOldBackup(string fileName)
		{
			FileInfo fileInfo = new FileInfo(fileName);

			fileName = fileInfo.Name; // Get the file name without the full path

			bool backupIsOld = false;

			char[] fileNameCharsArray = fileName.ToCharArray();

			string dateString = String.Empty;

			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < fileNameCharsArray.Length; i++)
			{
				if (Char.IsNumber(fileNameCharsArray[i]))
				{
					sb.Append(fileNameCharsArray[i]);
				}
			}

			dateString = sb.ToString();

			if (!String.IsNullOrEmpty(dateString))
			{
				// Delete only if we have exactly 8 digits
				if (dateString.Length == 8)
				{
					string year = String.Empty;
					string month = String.Empty;
					string day = String.Empty;

					year = dateString.Substring(0, 4);
					month = dateString.Substring(4, 2);
					day = dateString.Substring(6, 2);

					DateTime backupDate = new DateTime(int.Parse(year), int.Parse(month), int.Parse(day));

					int backupConsideredOldAfterDays = int.Parse(ConfigurationManager.AppSettings["DeleteBackupsAfterDays"].ToString());

					// Compare backup date to test if this backup 
					// should be treated as an old backup.
					TimeSpan backupAge = DateTime.Now.Subtract(backupDate);

					if (backupAge.Days > backupConsideredOldAfterDays)
					{
						backupIsOld = true;
					}
				}
			}

			return backupIsOld;
		}


		private bool islemZamaniGeldimi() {
		return true;
		}


		protected override void OnStart(string[] args)
		{
			try
			{
				Initialise();

				if (Directory.GetFiles(m_backupDir, "*.bak").Length == 0)
				{
					tmrFirtTime = new System.Timers.Timer();
					tmrFirtTime.Start();
					tmrFirtTime.AutoReset = false;
					tmrFirtTime.Interval = 30000;
					tmrFirtTime.Elapsed += new System.Timers.ElapsedEventHandler(this.tmrFirstTime_Tick);
					tmrFirtTime.Enabled = true;
				}
				Tmr1 = new System.Timers.Timer();
				Tmr1.Start();
				Tmr1.AutoReset = true;
				Tmr1.Interval = (int.Parse(ConfigurationManager.AppSettings["BackupTime"].ToString()));
				Tmr1.Elapsed += new System.Timers.ElapsedEventHandler(this.Tmr1_Tick);
				Tmr1.Enabled = true;
			}
			catch (Exception ex)
			{
				Output(ex.ToString());
				throw;
			}
			
		}

		public void Tmr1_Tick(object sender, EventArgs e)
		{
			HemoBackupService(); 
		}

		public void tmrFirstTime_Tick(object sender, EventArgs e)
		{
			HemoBackupService();
			tmrFirtTime.Enabled = false;
			tmrFirtTime = null;
		}
		
		protected override void OnStop()
		{
		}

		private void timer1_Tick(object sender, EventArgs e)
		{

		}
	}
	}
