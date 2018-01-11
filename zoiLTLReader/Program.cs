using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mastonet.Entities;
using Mastonet;
using System.Text.RegularExpressions;
using System.Configuration;

namespace zoiLTLReader
{
	class Program
	{
		private static Dictionary<string, string> nameList = new Dictionary<string, string>();
		private static string[] muteList;

		static void Main(string[] args)
		{
			Console.WriteLine("zoiLTL Reader");

			//名前固定読み上げリスト取得
			nameList = getNameList();
			Console.WriteLine("LOAD static name list");

			//ミュートリスト取得
			muteList = getMuteList();
			Console.WriteLine("LOAD mute user list");

			//丼認証関連
			var appRegistration = new AppRegistration
			{
				Instance = ConfigurationManager.AppSettings["instanceUrl"],
				ClientId = ConfigurationManager.AppSettings["clientID"],
				ClientSecret = ConfigurationManager.AppSettings["clientSecret"],
				Scope = Scope.Read
			};
			var authClient = new AuthenticationClient(appRegistration);
			var auth = authClient.ConnectWithPassword(ConfigurationManager.AppSettings["loginID"], ConfigurationManager.AppSettings["loginPassword"]).Result;
			var client = new MastodonClient(appRegistration, auth);
			Console.WriteLine("mastodon login ok");

			new Program().LTLStream(client);
			new Program().HomeStream(client);

			//exit入力で終了
			Console.WriteLine("exit入力で終了");
			while (true)
			{
				if (Console.ReadLine() == "exit")
				{
					break;
				}
				Console.WriteLine("exit入力で終了");
			}
		}

		private async Task LTLStream(MastodonClient client)
		{
			Console.WriteLine("LTL Connect");

			//LTLストリーム取得設定(mastonet改造拡張機能)
			var ltlStreaming = client.GetLocalStreaming();

			//htmlタグ除去
			var rejectHtmlTagReg = new Regex("<.*?>");

			ltlStreaming.OnUpdate += (sender, e) =>
			{
				//読み上げ対象外アカウント判定
				if (((IList<string>)muteList).Contains(e.Status.Account.UserName))
				{
					Console.WriteLine("読み上げ除外対象：" + e.Status.Account.UserName);
					return;
				}

				var userName = getNameRead(e.Status.Account.UserName, e.Status.Account.DisplayName);
				var toot = rejectHtmlTagReg.Replace(e.Status.Content, "");

				var attachMedia = false;
				foreach (var attachMediaList in e.Status.MediaAttachments)
				{
					//Console.WriteLine("添付ファイルあり");
					attachMedia = true;
					break;
				}

				var speakString = userName + " 、" + toot;

				//Console.WriteLine(userID);
				//Console.WriteLine(userName);
				//Console.WriteLine(toot);
				if (attachMedia)
				{
					//Console.WriteLine("添付ファイルあり");
					speakString += "、添付あり。";
				}

				Console.WriteLine(speakString);
				speakText(speakString);
			};
			await ltlStreaming.Start();
		}

		private async Task HomeStream(MastodonClient client)
		{
			Console.WriteLine("HOME Connect");

			//htmlタグ除去
			var rejectHtmlTagReg = new Regex("<.*?>");

			//Homeストリーム取得設定
			var homeStreaming = client.GetUserStreaming();
			homeStreaming.OnUpdate += (sender, e) =>
			{
				//公開tootは読み上げ対象外
				if (e.Status.Visibility == Mastonet.Visibility.Public)
				{
					return;
				}

				//読み上げ対象外アカウント判定
				if (((IList<string>)muteList).Contains(e.Status.Account.UserName))
				{
					Console.WriteLine("読み上げ除外対象：" + e.Status.Account.UserName);
					return;
				}

				var userName = getNameRead(e.Status.Account.UserName, e.Status.Account.DisplayName);
				var toot = rejectHtmlTagReg.Replace(e.Status.Content, "");

				var speakString ="ホーム限定、" + userName + " 、" + toot;

				var attachMedia = false;
				foreach (var attachMediaList in e.Status.MediaAttachments)
				{
					//Console.WriteLine("添付ファイルあり");
					attachMedia = true;
					break;
				}
				if (attachMedia)
				{
					//Console.WriteLine("添付ファイルあり");
					speakString += "、添付あり。";
				}
				Console.WriteLine(speakString);
				speakText(speakString);
			};
			await homeStreaming.Start();
		}

		private static Dictionary<string, string> getNameList()
		{
			var nameList = new Dictionary<string, string>();
			//CSVファイル名
			string csvFileName = "nameList.csv";

			//Shift JISで読み込む
			Microsoft.VisualBasic.FileIO.TextFieldParser tfp = new Microsoft.VisualBasic.FileIO.TextFieldParser(csvFileName, System.Text.Encoding.GetEncoding(932));

			//フィールドが文字で区切られているとする
			//デフォルトでDelimitedなので、必要なし
			tfp.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
			//区切り文字を,とする
			tfp.Delimiters = new string[] { "," };
			//フィールドを"で囲み、改行文字、区切り文字を含めることができるか
			//デフォルトでtrueなので、必要なし
			tfp.HasFieldsEnclosedInQuotes = true;
			//フィールドの前後からスペースを削除する
			//デフォルトでtrueなので、必要なし
			tfp.TrimWhiteSpace = true;

			while (!tfp.EndOfData)
			{
				//フィールドを読み込む
				string[] fields = tfp.ReadFields();
				//保存
				nameList.Add(fields[0], fields[1]);
			}
			tfp.Close();
			return nameList;
		}

		private static string[] getMuteList()
		{
			//ファイル名
			string textFile = "muteList.csv";
			//文字コード(SJIS)
			Encoding enc = Encoding.GetEncoding("shift_jis");

			//行ごとに配列へ格納
			string[] muteList = System.IO.File.ReadAllLines(textFile, enc);

			return muteList;
		}

		private string getNameRead(string id, string name)
		{
			try
			{
				return nameList[id];
			}
			catch (KeyNotFoundException)
			{
				return name;
			}
		}

		private void speakText(string text)
		{
			//棒読み
			var bouyomi = ConfigurationManager.AppSettings["bouyomiPath"];
			System.Diagnostics.Process p = new System.Diagnostics.Process();
			p.StartInfo.FileName = System.Environment.GetEnvironmentVariable("ComSpec");

			p.StartInfo.UseShellExecute = false;
			p.StartInfo.CreateNoWindow = true;

			//読み上げコマンド発行
			p.StartInfo.Arguments = "/c start /MIN " + bouyomi + " /T " + "\"" + text + "\"";
			p.Start();

		}
	}
}

