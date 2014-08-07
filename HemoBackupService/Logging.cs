using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace HemoBackupService
{
	public class LogWriter
	{


		public static bool WriteLogToTextFile(string loggingDirectoryPath, string logMessage)
		{
			bool successStatus = false;

			DateTime currentDateTime = DateTime.Now;

			string currentDateTimeString = currentDateTime.ToString();

			CheckCreateLogDirectory(loggingDirectoryPath);

			string logLine = BuildLogLine(currentDateTime, logMessage);

			loggingDirectoryPath = (loggingDirectoryPath + "Log_" + GetLogFileNameDateString(DateTime.Now) + ".txt");

			// Lock type as this is a static method / class
			lock (typeof(LogWriter))
			{
				StreamWriter m_logSWriter = null;

				try
				{
					m_logSWriter = new StreamWriter(loggingDirectoryPath, true);

					m_logSWriter.WriteLine(logLine);

					successStatus = true;
				}
				catch
				{
					// 
				}
				finally
				{
					if (m_logSWriter != null)
					{
						m_logSWriter.Close();
					}
				}
			}

			return successStatus;
		}


		/// <summary>
		/// Checks for the existence of a log file directory, and creates the directory
		/// if it is not found.
		/// </summary>
		/// <param name="logPath"></param>
		private static bool CheckCreateLogDirectory(string logPath)
		{
			bool loggingDirectoryExists = false;

			DirectoryInfo dirInfo = new DirectoryInfo(logPath);

			if (dirInfo.Exists)
			{
				loggingDirectoryExists = true;
			}
			else
			{
				try
				{
					Directory.CreateDirectory(logPath);

					loggingDirectoryExists = true;
				}
				catch
				{
					// Logging failure
				}
			}

			return loggingDirectoryExists;
		}


		/// <summary>
		/// Returns a string for insertion into the log. Columns separated by tabs.
		/// </summary>
		/// <param name="currentDateTime"></param>
		/// <param name="logMessage"></param>
		/// <returns></returns>
		private static string BuildLogLine(DateTime currentDateTime, string logMessage)
		{
			StringBuilder loglineStringBuilder = new StringBuilder();

			loglineStringBuilder.Append(GetLogFileEntryDateString(currentDateTime));
			loglineStringBuilder.Append("\t");
			loglineStringBuilder.Append(logMessage);

			return loglineStringBuilder.ToString();
		}


		/// <summary>
		/// Returns a string with the date in the format yyyy-MM-dd HH:mm:ss
		/// </summary>
		/// <param name="currentDateTime"></param>
		/// <returns></returns>
		public static string GetLogFileEntryDateString(DateTime currentDateTime)
		{
			return currentDateTime.ToString("yyyy-MM-dd HH:mm:ss");
		}

		/// <summary>
		/// Returns a string with the date in the format yyyy_MM_dd
		/// </summary>
		/// <param name="currentDateTime"></param>
		/// <returns></returns>
		private static string GetLogFileNameDateString(DateTime currentDateTime)
		{
			return currentDateTime.ToString("yyyy_MM_dd");
		}
	}
}
