using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;
using ImageMagick;
using Kea.CommonFiles;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace Kea
{
	public partial class Main : Form
	{
		public const int WM_NCLBUTTONDOWN = 0xA1;
		public const int HT_CAPTION = 0x2;

		public List<Structures.ToonListEntry> toonList;
		public string saveAs;
		public string animeImageUrl;
        public int animeMaxChapter;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool ReleaseCapture();

		private const int CS_DROPSHADOW = 0x00020000;
		protected override CreateParams CreateParams
		{
			get
			{
				// add the drop shadow flag for automatically drawing
				// a drop shadow around the form
				CreateParams cp = base.CreateParams;
				cp.ClassStyle |= CS_DROPSHADOW;
				return cp;
			}
		}

		public Main()
		{
			InitializeComponent();
			QueueGrid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
			saveAsOption.DropDownStyle = ComboBoxStyle.DropDownList;
			//toolTips.SetToolTip(oneImagecb, "If the image of a chapter exceeds\n30,000 pixels it will be down scaled");
		}

		private void HandleBar_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)  //allow moving of the window
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
			}
		}

		private void addToQueueBtn_Click(object sender, EventArgs e)
        {//ex: https://www.webtoons.com/en/action/hero-killer/list?title_no=2745
         //ex:https://anime-sama.fr/catalogue/one-piece/scan_noir-et-blanc/vf/
            List<string> lines = new List<string>();
			lines.AddRange(URLTextbox.Text.Split('\n'));
			foreach (string _line in lines)
			{
				string line = _line;
				int nameEnd = 0;
				int nameStart = 0;
                Uri lineUri = new Uri(line);
                string languageCode = System.Web.HttpUtility.ParseQueryString(lineUri.Query).Get("language");

                if (line.Contains("https://www.webtoons.com/") && line.Contains("/list?title_no="))
				{
                    if (line.Length - line.Replace("/", "").Length != 6) { continue; }
                    try
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            nameStart = nameEnd;
                            while (line[nameEnd] != '/') nameEnd++;
                            nameEnd++;
                        }
                    }
                    catch { continue; }
                    string toonName = line.Substring(nameStart, nameEnd - nameStart - 1);

                    
                    int titleNo = Convert.ToInt32(System.Web.HttpUtility.ParseQueryString(lineUri.Query).Get("title_no"));


                    if (Helpers.IsStringEmptyNullOrWhiteSpace(languageCode))
                    {
                        languageCode = "default";
                    }
                    else
                    {
                        //Query used by Kea to download fan translations, webtoons doesn't support this query
                        line = Helpers.RemoveQueryStringByKey(line, "language");
                    }

                    string teamVersion = System.Web.HttpUtility.ParseQueryString(lineUri.Query).Get("teamVersion");

                    if (Helpers.IsStringEmptyNullOrWhiteSpace(teamVersion))
                    {
                        teamVersion = "default";
                    }
                    else
                    {
                        //Query used by Kea to download fan translations, webtoons doesn't support this query
                        line = Helpers.RemoveQueryStringByKey(line, "teamVersion");
                    }

                    if (languageCode == "default" && teamVersion != "default")
                    {
                        MessageBox.Show("default language can't have a team version.");
                        continue;
                    }

                    var items = QueueGrid.Rows.Cast<DataGridViewRow>().Where(row => row.Cells["titleName"].Value.ToString() == toonName && Convert.ToInt32(row.Cells["titleNo"].Value.ToString()) == titleNo && row.Cells["titleTranslationLanguageCode"].Value.ToString() == languageCode && row.Cells["titleTranslationTeamVersion"].Value.ToString() == teamVersion);

                    if (items.Count() != 0)
                        continue;

                    QueueGrid.Rows.Add(titleNo, toonName, "1", "end", languageCode, teamVersion, line);
                }
                else if (line.Contains("https://anime-sama.fr"))
				{
					string toonName = line.Split('/')[4];
                    QueueGrid.Rows.Add(0, toonName, "1", "end", "default", "default", line);
                }
				else
				{
					continue;
				}

                
			}
			URLTextbox.Text = "";
		}

		private async void startBtn_Click(object sender, EventArgs e)
		{
			DisableAllControls(this);
			saveAs = saveAsOption.Text;
			
			if (skipDownloadedChaptersCB.Checked && saveAs == "multiple images")
			{
				MessageBox.Show("Skipping downloaded chapters cannot be used while saving as \"multiple images\" ");
				EnableAllControls(this);
				return;
			}

			bool wasWarned = false;
			foreach (DataGridViewRow r in QueueGrid.Rows)
			{
				int end = 0, start = 0;
				try
				{
					start = int.Parse(r.Cells["titleEpBegin"].Value.ToString());
					if (start < 1) { MessageBox.Show("The start chapter must be greater than zero!"); EnableAllControls(this); return; }
				}
				catch { MessageBox.Show("The start chapter must be a number!"); EnableAllControls(this); return; }

				try
				{
					end = int.Parse(r.Cells["titleEpEnd"].Value.ToString());
					if (end < 1) { MessageBox.Show("The end chapter must be greater than zero!"); EnableAllControls(this); return; }
				}
				catch
				{
					if (r.Cells["titleEpEnd"].Value.ToString() != "end") { MessageBox.Show("The end chapter must be a number or the word 'end'!"); EnableAllControls(this); return; }
				}
				if (end != 0 && end < start) { MessageBox.Show("The start chapter must smaller than the end chapter!"); EnableAllControls(this); return; }
				if( !wasWarned && HighestQualityCB.Checked && r.Cells["titleTranslationLanguageCode"].Value.ToString() != "default" )
				{
					MessageBox.Show("Warning! High quality options will be ignored for fan translations.");
					wasWarned=true;
				}
			}
			EnableControls(HandleBar);
			EnableControls(exitBtn);
			EnableControls(minimizeBtn);
			await DownloadQueueAsync();
			EnableAllControls(this);
			if (saveAs != "multiple images") chapterFoldersCB.Enabled = false;
		}

		private async Task DownloadQueueAsync()
		{
			if (!savepathTB.Text.Contains('\\'))
			{
				savepathTB.Text = "please select a directory for saving";
				return;
			}
			if (QueueGrid.Rows.Count == 0) return;


			if (QueueGrid.Rows[0].Cells["titleUrl"].Value.ToString().Contains("anime"))
			{
                await DownloadAnimeListAsync();
            }
			else
			{
                await DownloadWebtoonAsync();
            }

            processInfo.Text = "done!";
			progressBar.Value = progressBar.Minimum;
		}

        private async Task DownloadAnimeListAsync()
        {
            foreach (DataGridViewRow r in QueueGrid.Rows) //get all chapter links
            {
                //Début et fin
                int chapter = int.Parse(r.Cells["titleEpBegin"].Value.ToString());
                int chapterEnd = (r.Cells["titleEpEnd"].Value.ToString() == "end") ? -1 : int.Parse(r.Cells["titleEpEnd"].Value.ToString());
                DownloadAnimeAsync(r.Cells["titleUrl"].Value.ToString(), chapter, chapterEnd);

            }

        }

        private async Task DownloadAnimeAsync(string url, int chapter, int chapterEnd)
        {
            //Récupération d'une image 
            //lancement du process pour l'anime concerné
            await GetImagePathAsync(url);
			if (chapterEnd == -1)
			{
				chapterEnd = animeMaxChapter;
            }

            //Occurence suivante si non tve
            if (string.IsNullOrEmpty(animeImageUrl)) { return; }

            string imgExtension = Helpers.GetFileExtensionFromUrl(animeImageUrl);
            string[] urlSplit = animeImageUrl.Split('/');
            //on enlève 1/1.jpg
            int sufixSize = urlSplit[urlSplit.Length - 2].Length + 1 + urlSplit[urlSplit.Length - 1].Length;
            string baseUrl = animeImageUrl.Remove(animeImageUrl.Length - sufixSize, sufixSize);

            string baseSavePath = savepathTB.Text + @"\";
			string animeName = urlSplit[urlSplit.Length - 3].Replace("%20", " ");
            string comicSavePath = baseSavePath + animeName;
            string comicErrorSavePath = comicSavePath + "_Error";

            //Création des repertoirs de travail
            Directory.CreateDirectory(string.Format("{0}\\", comicSavePath));
            Directory.CreateDirectory(string.Format("{0}\\", comicErrorSavePath));
            List<Structures.downloadedToonChapterFileInfo> downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();
			int prevChapter = chapter;
            bool isEnded = false;

            processInfo.Invoke((MethodInvoker)delegate
            {
                progressBar.Minimum = chapter;
                progressBar.Maximum = chapterEnd;
            });

            while (!isEnded && (chapter <= chapterEnd))
            {
                processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({animeName}) ] treating chapter {chapter}/{chapterEnd}"; try { progressBar.Value = chapter; } catch { } }); //run on the UI thread

                //faire traitement chapitre(chapter) , renvoie false quand rien n'est trouvé (pour sortir de la boucle)
                List<Structures.downloadedToonChapterFileInfo> downloadedChapter = ChapterTreatment(chapter, comicSavePath, comicErrorSavePath, baseUrl,imgExtension);
				isEnded = downloadedChapter.Count == 0;
				downloadedImages.AddRange(downloadedChapter);

				//1 doc par 10 chapitre
                if (chapter % 10 == 0 || isEnded || chapter == chapterEnd)
                {
                    ToonHelpers.createBundledFile(saveAs, string.Format("{0}_{1:D3}_{2:D3}", comicSavePath, prevChapter, chapter), downloadedImages);
					prevChapter = chapter + 1;
                    downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();
                }
                chapter++;
            }

            //Suppression du répertoire de travail
            Directory.Delete(comicSavePath, true);
            //Si vide, suppression du répertoire des anomalies
            if (Directory.GetFiles(comicErrorSavePath).Length == 0)
            {
                Directory.Delete(comicErrorSavePath, true);
            }


        }

        private List<Structures.downloadedToonChapterFileInfo> ChapterTreatment(int chapter, string comicSavePath,string comicErrorSavePath, string baseUrl, string imgExtension)
        {
			bool isOk = true;
            List<Structures.downloadedToonChapterFileInfo> downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();
            Bitmap bitmap = null;

            string chapterSavePath = string.Format("{0}\\", comicSavePath);

            int frame = 1;

            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Cookie", "pagGDPR=true;");  //add cookies to bypass age verification
                client.Headers.Add("User-Agent", Globals.spoofedUserAgent);

                IWebProxy proxy = WebRequest.DefaultWebProxy;   //add default proxy
                client.Proxy = proxy;

                client.Encoding = System.Text.Encoding.UTF8;

                while (isOk)
                {
                    Structures.downloadedToonChapterFileInfo fileInfo = new Structures.downloadedToonChapterFileInfo();

                    string imgName = string.Format("{0:D5}_{1:D5}", chapter, frame);
                    string imgSaveName = $"{imgName}{imgExtension}";
                    string imgSavePath = $"{chapterSavePath}{imgSaveName}";
                    string imgUrl = string.Format("{0}{1}/{2}{3}", baseUrl, chapter, frame, imgExtension);

                    try
                    {
                        client.DownloadFile(new Uri(imgUrl), imgSavePath);
                        frame++;

                        try
                        {
                            bitmap = (Bitmap)System.Drawing.Image.FromFile(imgSavePath);

                            //Ratio 0.69 = paysage, 1,43 portrait
							//Si on dépasse ce ration, il y a surement plusieurs scan dans l'image
                            double ratio = (double)bitmap.Height / bitmap.Width;
                            if ((ratio < 0.89 && ratio > 0.49) ||
                                (ratio < 1.63 && ratio > 1.23))
                            {
                                fileInfo.filePath = imgSavePath;
                                fileInfo.filePathInArchive = imgSaveName;
                                downloadedImages.Add(fileInfo);

                            }
                            else
                            {
								//Regarder s'il y a plusieurs images dans le scan
                                double d = ((double)bitmap.Height / bitmap.Width) / 1.43;///////////////////////////////////////////////////////////JJK 1.5

								//Nombre d'images estimées
                                int nbImg = (int)Math.Round(d, MidpointRounding.ToEven);
								//Taille estimée d'une image
                                int newHeight = bitmap.Height / nbImg;

								//On créait des sous images pour remplacer l'original
                                for (int i = 0; i < nbImg; i++)
                                {
                                    Bitmap temp = bitmap.Clone(new System.Drawing.Rectangle(0, newHeight * i, bitmap.Width, newHeight), bitmap.PixelFormat);
                                    string tempName = $"{imgName}_{i:D2}{imgExtension}";
                                    string tempPath = $"{chapterSavePath}{tempName}";

                                    temp.Save(tempPath);
                                    fileInfo.filePath = tempPath;
                                    fileInfo.filePathInArchive = tempName;
                                    downloadedImages.Add(fileInfo);
                                    temp.Dispose();
                                }

                            }
                            bitmap.Dispose();

                        }
                        catch (Exception)
                        {

                            if (bitmap != null)
                                bitmap.Dispose();

							string errorImgPath = string.Format("{0}\\{1}.webp", comicErrorSavePath, imgSaveName);
                            //On met les erreurs dans un repertoire spécifique non supprimé
                            File.Move(imgSavePath, errorImgPath);

							try
							{
                                // Charger l'image WEBP
                                using (var image = new MagickImage(errorImgPath))
                                {
                                    // Convertir au format JPG
                                    image.Format = MagickFormat.Jpg;

                                    // Sauvegarder l'image convertie
                                    image.Write(imgSavePath);
                                }
                                fileInfo.filePath = imgSavePath;
                                fileInfo.filePathInArchive = imgSaveName;
                                downloadedImages.Add(fileInfo);

								//Suppression de l'erreur bien traitée
								File.Delete(errorImgPath);
                            }
							catch (Exception)
							{ 
								//ne rien faire si anomalie
							}

                        }

                    }
                    catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
                    {
                        isOk = false;
                    }
                }
            }
			return downloadedImages;
        }

        private async Task DownloadWebtoonAsync()
        {
            toonList = new List<Structures.ToonListEntry>();

            foreach (DataGridViewRow r in QueueGrid.Rows) //get all chapter links
            {
                await Task.Run(() => GetChapterAsync(r));
            }
            for (int t = 0; t < toonList.Count; t++)    //for each comic in queue...
            {
                await Task.Run(() => downloadComic(toonList[t]));
            }
        }

        private async Task GetChapterAsync(DataGridViewRow r)
		{
			string line = r.Cells["titleUrl"].Value.ToString();
			if (Helpers.IsStringEmptyNullOrWhiteSpace(line)) return;
			
			Structures.ToonListEntry currentToonEntry = new Structures.ToonListEntry();
			List<Structures.EpisodeListEntry> toonEpisodeList = new List<Structures.EpisodeListEntry>();
			
			int urlEnd = (line.IndexOf('&') == -1) ? line.Length : line.IndexOf('&');
			line = line.Substring(0, urlEnd);
			Uri baseUri = new Uri(line);
			string baseUrl = baseUri.GetLeftPart(UriPartial.Path);

			currentToonEntry.toonInfo.titleNo = Convert.ToInt32(r.Cells["titleNo"].Value.ToString());
			currentToonEntry.toonInfo.toonTitleName = r.Cells["titleName"].Value.ToString();
			currentToonEntry.toonInfo.startDownloadAtEpisode = r.Cells["titleEpBegin"].Value.ToString();
			currentToonEntry.toonInfo.stopDownloadAtEpisode = r.Cells["titleEpEnd"].Value.ToString();
			currentToonEntry.toonInfo.toonTranslationLanguageCode = r.Cells["titleTranslationLanguageCode"].Value.ToString();
			currentToonEntry.toonInfo.toonTranslationTeamVersion = r.Cells["titleTranslationTeamVersion"].Value.ToString();

			using (WebClient client = new WebClient())
			{
				int i = 0;
				
				string nextPageUrl = line + "&page=1";

				int episodeBegin = int.Parse(currentToonEntry.toonInfo.startDownloadAtEpisode);
				int episodeEnd = (currentToonEntry.toonInfo.stopDownloadAtEpisode == "end") ? -1 : int.Parse(currentToonEntry.toonInfo.stopDownloadAtEpisode);

				while (true)
				{
					i++;
					processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToonEntry.toonInfo.titleNo}) {currentToonEntry.toonInfo.toonTitleName} ] scoping tab {i}"; }); //run on the UI thread
					client.Headers.Add("Cookie", "pagGDPR=true;");  //add cookies to bypass age verification
					client.Headers.Add("User-Agent", Globals.spoofedUserAgent);

					IWebProxy proxy = WebRequest.DefaultWebProxy;   //add default proxy
					client.Proxy = proxy;
					
					client.Encoding = System.Text.Encoding.UTF8;

					string html = client.DownloadString(nextPageUrl);
					var htmlDoc = new HtmlAgilityPack.HtmlDocument();
					htmlDoc.LoadHtml(html);
					var episodeNodes = htmlDoc.DocumentNode.SelectNodes(Globals.episodeListItemHtmlXPath);

					if(episodeNodes != null)
					{
						foreach (var node in episodeNodes)
						{
							int episodeNo = -1;
							if (node.Attributes["data-episode-no"] != null)
							{
								episodeNo = Convert.ToInt32(node.Attributes["data-episode-no"].Value);
							}

							HtmlNode inner_a_node = node.SelectSingleNode("./a");
							string url = inner_a_node.Attributes["href"].Value;
							string episodeTitle = inner_a_node.SelectSingleNode("./span[@class='subj']/span").InnerHtml;
							string episodeSequence = inner_a_node.SelectSingleNode("./span[@class='tx']").InnerHtml;

							//Skip out-of-range chapters.
							if (episodeNo < episodeBegin || (episodeEnd != -1 && episodeNo > episodeEnd))
							{
								continue;
							}

							Structures.EpisodeListEntry currentEpisode = new Structures.EpisodeListEntry();
							
							currentEpisode.episodeSequence = episodeSequence;
							currentEpisode.episodeNo = episodeNo;
							currentEpisode.episodeTitle = Helpers.SanitizeStringForFilePath(episodeTitle);
							currentEpisode.url = url;
							
							toonEpisodeList.Add(currentEpisode);
						}
					}

					string nextPage = GetWebsiteNextPageUrl(htmlDoc);
					if (Helpers.IsStringEmptyNullOrWhiteSpace(nextPage))
						break;

					if (!Helpers.IsValidURL(nextPage))
						nextPage = baseUri.GetLeftPart(UriPartial.Authority) + nextPage;

					nextPageUrl = nextPage;
				}
			}
			
			//Toons are listed from last episode to first episode, so order needs to be reversed.
			toonEpisodeList.Reverse();

			currentToonEntry.episodeList = toonEpisodeList.ToArray();
			toonList.Add(currentToonEntry);
		}

        private async Task GetImagePathAsync(string line)
        {
            if (Helpers.IsStringEmptyNullOrWhiteSpace(line)) return;

            // Configurer le service pour ChromeDriver
            var chromeService = ChromeDriverService.CreateDefaultService();
            chromeService.HideCommandPromptWindow = true; // Masquer la console CMD

            // Configurer ChromeDriver
            var options = new ChromeOptions();
            options.AddArgument("--headless"); // Exécuter sans interface graphique

            using (var driver = new ChromeDriver(chromeService,options))
            {
                // Charger la page
                driver.Navigate().GoToUrl(line);

                // Attendre que les éléments soient chargés (optionnel, dépend du site)
                //System.Threading.Thread.Sleep(5000);

                // Récupérer les URLs des images dans la div id="scansPlacement"
                var imageElements = driver.FindElements(By.CssSelector("#scansPlacement img"));

				if(imageElements.Count == 0)
				{
					animeImageUrl = string.Empty;

                }
				else
				{
                    animeImageUrl = imageElements[0].GetAttribute("src");

                }

				//Récupération du nombre de chapitres existants
                // Localiser l'élément <select> par son ID
                var selectElement = driver.FindElement(By.Id("selectChapitres"));

                // Créer un objet SelectElement
                var select = new SelectElement(selectElement);

                // Compter le nombre d'options dans le menu déroulant
                animeMaxChapter = select.Options.Count;
            }
        }

        private void downloadComic(Structures.ToonListEntry currentToon)
		{
			string baseSavePath = savepathTB.Text + @"\";
			string comicSavePath = baseSavePath + ToonHelpers.GetToonSavePath(currentToon.toonInfo);

			if (cartoonFoldersCB.Checked)
			{
				//If checked, add the path separator & create new directory
				comicSavePath += @"\";
				Directory.CreateDirectory(comicSavePath); 
			}
			else
			{
				//separation isn't necessary here
				//comicSavePath += "_";
			}

			string suffix = "";
			if(HighestQualityCB.Checked && currentToon.toonInfo.toonTranslationLanguageCode == "default")
			{
				suffix = "[HQ]";
			}

			//set start and end chapter
			float startNr = 0;
			float endNr = currentToon.episodeList.Length;

			processInfo.Invoke((MethodInvoker)delegate
			{
				progressBar.Minimum = (int)startNr * 100;
				progressBar.Maximum = (int)endNr * 100;
			});

			for (int i = (int)startNr; i < (int)endNr; i++)	//...and for each chapter in that comic...
			{
				int episodeNo = currentToon.episodeList[i].episodeNo;
				
				processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] grabbing the html of chapter {episodeNo}"; try { progressBar.Value = i * 100; } catch { } }); //run on the UI thread
				
				string episodeSavePath = comicSavePath + ToonHelpers.GetToonEpisodeSavePath(currentToon.episodeList[i],suffix);
				string archiveSavePath = episodeSavePath; // shouldn't end with /
				
				if( skipDownloadedChaptersCB.Checked )
				{
					string bundlePath = $"{archiveSavePath}{ToonHelpers.GetBundleExtension(saveAs)}";
					//TODO: don't check for episode sequence
					if (File.Exists(bundlePath))
					{
						processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] Skipping chapter {episodeNo}";}); //run on the UI thread
						continue;
					}
				}
				
				bool chapterDirectoryWasCreated = false;
				if (chapterFoldersCB.Checked || saveAs != "multiple images")
				{
					//If checked, add the path separator & create new directory
					episodeSavePath += @"\";
					Directory.CreateDirectory(episodeSavePath);
					chapterDirectoryWasCreated = true;
				}
				else
				{
					episodeSavePath += "_"; // separate between episode name & image number
				}
				
				List<Structures.downloadedToonChapterFileInfo> downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();
				int imageNo = 0;
				
				using (WebClient client = new WebClient())
				{
					client.Headers.Add("Cookie", "pagGDPR=true;");  //add cookies to bypass age verification
					client.Headers.Add("User-Agent", Globals.spoofedUserAgent);

					IWebProxy proxy = WebRequest.DefaultWebProxy;	//add default proxy
					client.Proxy = proxy;
					
					client.Encoding = System.Text.Encoding.UTF8;
					
					string html = client.DownloadString(currentToon.episodeList[i].url);
					HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
					doc.LoadHtml(html);
					
					string[] imgUrlArray = new string[] {  };
					
					if( currentToon.toonInfo.toonTranslationLanguageCode == "default" )
					{
						//Download official translation

						List<string> imgUrlList = new List<string>();

						var episodeImgs = doc.DocumentNode.SelectNodes(Globals.episodeImageHtmlXPath);
						//In case SelectNodes messes the order, sort by position in html document
						HtmlNode[] imgList = episodeImgs.OrderBy(node => node.StreamPosition).ToArray();
						foreach (HtmlNode imageNode in imgList)
						{
							string imgUrl = imageNode.Attributes["data-url"].Value;
							imgUrlList.Add( imgUrl );
						}
						imgUrlArray = imgUrlList.ToArray();
					}
					else
					{
						//Download fan translation
						List<string> imgUrlList = new List<string>();

						//Find available translations
						string jsonResponse = client.DownloadString( $"{Globals.naverWebtoonAPIBaseUrl}/ctrans/translatedEpisodeLanguageInfo_jsonp.json?titleNo={currentToon.toonInfo.titleNo}&episodeNo={episodeNo}" );
						JObject o = JObject.Parse(jsonResponse);
						string selectCondition = "@.languageCode == '" + currentToon.toonInfo.toonTranslationLanguageCode + "'";
						if (currentToon.toonInfo.toonTranslationTeamVersion != "default" )
						{
							selectCondition += " && @.teamVersion == " + currentToon.toonInfo.toonTranslationTeamVersion;
						}
						IEnumerable<JToken> languagesObject = o.SelectTokens("$.result.languageList[?(" + selectCondition + ")]").OrderByDescending(r => r["likeItCount"]);
						JToken selectedTranslation = languagesObject.FirstOrDefault();
						//If no translation was found, chapter will be skipped because image list is empty
						if (selectedTranslation != null)
						{
							string teamName = selectedTranslation["teamName"].ToString();
							string teamVersion = selectedTranslation["teamVersion"].ToString();
							string languageName = selectedTranslation["languageName"].ToString();

							//Get image list of selected translation
							string imageListJsonResponse = client.DownloadString($"{Globals.naverWebtoonAPIBaseUrl}/ctrans/translatedEpisodeDetail_jsonp.json?titleNo={currentToon.toonInfo.titleNo}&episodeNo={episodeNo}&languageCode={currentToon.toonInfo.toonTranslationLanguageCode}&teamVersion={teamVersion}");
							JObject imageListO = JObject.Parse(imageListJsonResponse);
							IEnumerable<JToken> imageInfo = imageListO.SelectTokens("$.result.imageInfo[*]").OrderBy(r => r["sortOrder"]);
							foreach (JToken currentImageInfo in imageInfo)
							{
								imgUrlList.Add(currentImageInfo["imageUrl"].ToString());
							}
							imgUrlArray = imgUrlList.ToArray();
							//If the chapter contains an image
							//If translation exists
							//Include a non-official warning
							if( imgUrlArray.Length > 0 )
							{
								Structures.downloadedToonChapterFileInfo fileInfo = new Structures.downloadedToonChapterFileInfo();
								string imgName = imageNo.ToString("D5");
								string imgSaveName = $"{imgName}.jpg";
								string imgSavePath = $"{episodeSavePath}{imgSaveName}";
								
								ToonHelpers.DrawAndSaveUnofficialWarningImage(languageName,teamName,imgSavePath);
								fileInfo.filePath = imgSavePath;
								fileInfo.filePathInArchive = imgSaveName;
								downloadedImages.Add(fileInfo);
								
								imageNo++;
							}
						}
					}

					int totalImgCount = imgUrlArray.Length;

					if( totalImgCount == 0 )
						continue;
					
					totalImgCount += imageNo; //include generated images
					
					bool hasFailed = false;
					foreach (string _imgUrl in imgUrlArray)
					{
						string imgUrl = _imgUrl;
						processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] downloading image {imageNo} of chapter {episodeNo}!"; }); //run on the UI thread
						client.Headers.Add("Referer", currentToon.episodeList[i].url);	//refresh the referer for each request!

						string imgName = imageNo.ToString("D5");
						if(HighestQualityCB.Checked && currentToon.toonInfo.toonTranslationLanguageCode == "default")
						{
							//Remove the "?type=" query string from image url, this results in downloading the image with the same quality stored in the server.
							imgUrl = Helpers.RemoveQueryStringByKey(imgUrl, "type");
							imgName += "[HQ]";
						}
						
						Structures.downloadedToonChapterFileInfo fileInfo = new Structures.downloadedToonChapterFileInfo();
						
						string imgExtension = Helpers.GetFileExtensionFromUrl(imgUrl);
						
						string imgSaveName = $"{imgName}{imgExtension}";
						string imgSavePath = $"{episodeSavePath}{imgSaveName}";

						try{ client.DownloadFile(new Uri(imgUrl), imgSavePath); }
						catch(WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
						{
							// handle file not found
							imgSaveName = $"{imgName}_failed.png";
							imgSavePath = $"{episodeSavePath}{imgSaveName}";
							ToonHelpers.DrawAndSaveNotFoundImage(imageNo,imgSavePath);
							hasFailed = true;
						}

						fileInfo.filePath = imgSavePath;
						fileInfo.filePathInArchive = imgSaveName;
						downloadedImages.Add(fileInfo);
						
						processInfo.Invoke((MethodInvoker)delegate { try { progressBar.Value = i * 100 + (int)(imageNo / (float)totalImgCount * 100); } catch { } });
						imageNo++;
					}

					if( hasFailed )
						archiveSavePath += "_failed";
				}
				
				ToonHelpers.createBundledFile(saveAs, archiveSavePath, downloadedImages );
				if( chapterDirectoryWasCreated && ToonHelpers.isBundle(saveAs) )
				{
					Directory.Delete(episodeSavePath, true);
				}
			}
		}

		public static string GetWebsiteNextPageUrl(HtmlAgilityPack.HtmlDocument htmlDoc)
		{
			var pageNodes = htmlDoc.DocumentNode.SelectNodes(Globals.episodeListPaginatorXPath);
			var nextPageUrl = "";

			if (pageNodes == null)
				return "";

			bool bGetNextPage = false;
			foreach (var node in pageNodes)
			{
				if (node.Attributes["href"] != null)
				{
					if (bGetNextPage)
					{
						nextPageUrl = node.Attributes["href"].Value;
						bGetNextPage = false;
						break;
					}

					if (node.Attributes["href"].Value == "#")
						bGetNextPage = true;
				}
			}
			return nextPageUrl;
		}

		#region visuals
		private void exitBtn_Click(object sender, EventArgs e) { Application.Exit(); } //c'mon man, isn't this obvious
		private void exitBtn_MouseEnter(object sender, EventArgs e) { exitBtn.BackColor = Color.FromArgb(255, 20, 70, 34); }
		private void exitBtn_MouseLeave(object sender, EventArgs e) { exitBtn.BackColor = Color.FromArgb(255, 0, 30, 14); }

		private void minimizeBtn_Click(object sender, EventArgs e) { WindowState = FormWindowState.Minimized; } //c'mon man, isn't this obvious
		private void minimizeBtn_MouseEnter(object sender, EventArgs e) { minimizeBtn.BackColor = Color.FromArgb(255, 20, 70, 34); }
		private void minimizeBtn_MouseLeave(object sender, EventArgs e) { minimizeBtn.BackColor = Color.FromArgb(255, 0, 30, 14); }

		private void selectFolderBtn_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofile = new OpenFileDialog
			{
				ValidateNames = false,
				CheckFileExists = false,
				CheckPathExists = true,
				FileName = "Folder Selection"
			};
			if (DialogResult.OK == ofile.ShowDialog())
			{
				savepathTB.Text = Path.GetDirectoryName(ofile.FileName);
			}
		}


		private void removeAllBtn_Click(object sender, EventArgs e)
		{
			QueueGrid.Rows.Clear();
		}

		private void removeSelectedBtn_Click(object sender, EventArgs e)
		{
			if (QueueGrid.Rows.Count == 0) return;

			QueueGrid.Rows.RemoveAt(QueueGrid.SelectedRows[0].Index);
		}

		private void DisableAllControls(Control con)
		{
			foreach (Control c in con.Controls)
			{
				DisableAllControls(c);
			}
			con.Enabled = false;
		}

		private void helpBtn_Click(object sender, EventArgs e)
		{
			Process.Start("https://github.com/RustingRobot/Kea#how-to-use");
		}

		private void saveAsOption_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (saveAsOption.Text == "multiple images")
			{
				chapterFoldersCB.Enabled = true;
				chapterFoldersCB.Checked = true;
			}
			else
			{
				chapterFoldersCB.Enabled = false;
				chapterFoldersCB.Checked = false;
			}
		}

		private void EnableAllControls(Control con)
		{
			foreach (Control c in con.Controls)
			{
				EnableAllControls(c);
			}
			con.Enabled = true;
		}

		private void EnableControls(Control con)
		{
			if (con != null)
			{
				con.Enabled = true;
				EnableControls(con.Parent);
			}
		}
        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
			/*
			 * - Faire fonctionner avec l'écran actuel (recherche des images dans l'url via regex)
			 * - Ajouter des exemples dans l'écran
			 * - Split en plusieurs fonction nommées
			 * - Commenter
			 * - Mettre sur GIT
			 * - Voir pour gérer avec visualisation le système de découpage
			 * - Voir pour corriger les images buggées
			 * 
			 * */

			bool isOk = true;
            saveAs = saveAsOption.Text;
            string imgExtension = Helpers.GetFileExtensionFromUrl(URLTextbox.Text);
			string[] urlSplit = URLTextbox.Text.Split('/');
			//on enlève 1/1.jpg
			int sufixSize = urlSplit[urlSplit.Length - 2].Length + 1 + urlSplit[urlSplit.Length - 1].Length;	
            string baseUrl = URLTextbox.Text.Remove(URLTextbox.Text.Length - sufixSize, sufixSize);

            string baseSavePath = savepathTB.Text + @"\";
            string comicSavePath = baseSavePath + urlSplit[urlSplit.Length - 3];
            string comicErrorSavePath = baseSavePath + urlSplit[urlSplit.Length - 3]+ "_Error";
            Directory.CreateDirectory(string.Format("{0}\\", comicSavePath));
            Directory.CreateDirectory(string.Format("{0}\\", comicErrorSavePath));
            List<Structures.downloadedToonChapterFileInfo> downloadedImages;
            downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();
            int chapter = 1;
			int frame = 1;
			Bitmap bitmap = null;


            using (WebClient client = new WebClient())
			{
                client.Headers.Add("Cookie", "pagGDPR=true;");  //add cookies to bypass age verification
				client.Headers.Add("User-Agent", Globals.spoofedUserAgent);

				IWebProxy proxy = WebRequest.DefaultWebProxy;   //add default proxy
				client.Proxy = proxy;

				client.Encoding = System.Text.Encoding.UTF8;

				while (isOk)
				{
                    Structures.downloadedToonChapterFileInfo fileInfo = new Structures.downloadedToonChapterFileInfo();

					string imgName = string.Format("{0:D5}_{1:D5}",chapter, frame);
					string chapterSavePath = string.Format("{0}\\", comicSavePath);


					string imgSaveName = $"{imgName}{imgExtension}";
					string imgSavePath = $"{chapterSavePath}{imgSaveName}";

                    string imgUrl = string.Format("{0}{1}/{2}{3}", baseUrl, chapter,frame, imgExtension);

                    try { 
						client.DownloadFile(new Uri(imgUrl), imgSavePath);
						frame++;

						try
						{
                            bitmap = (Bitmap)System.Drawing.Image.FromFile(imgSavePath);

                            //Ratio 0.69 = paysage, 1,43 portrait
							double ratio = (double)bitmap.Height / bitmap.Width;
                            if ((ratio < 0.89 && ratio >0.49) ||
                                (ratio < 1.63 && ratio > 1.23))
                            {
                                fileInfo.filePath = imgSavePath;
                                fileInfo.filePathInArchive = imgSaveName;
                                downloadedImages.Add(fileInfo);

                            }
                            else
							{
								double d = ((double)bitmap.Height / bitmap.Width) / 1.43;///////////////////////////////////////////////////////////JJK 1.5

                                int nbImg = (int)Math.Round(d, MidpointRounding.ToEven);
								int newHeight = bitmap.Height / nbImg;

                                for (int i = 0; i < nbImg; i++)
								{
									Bitmap temp = bitmap.Clone(new System.Drawing.Rectangle(0, newHeight*i, bitmap.Width, newHeight), bitmap.PixelFormat);
                                    string tempName = $"{imgName}_{i:D2}{imgExtension}";
                                    string tempPath = $"{chapterSavePath}{tempName}";

                                    temp.Save(tempPath);
                                    fileInfo.filePath = tempPath;
                                    fileInfo.filePathInArchive = tempName;
                                    downloadedImages.Add(fileInfo);
									temp.Dispose();
                                }

                            }

                        }
                        catch (Exception )
                        {
							if(bitmap!=null)
								bitmap.Dispose();

							//On met les erreurs dans un repertoire spécifique non supprimé
                            File.Move(imgSavePath, string.Format("{0}\\{1}", comicErrorSavePath, imgSaveName));
						}

                    }
					catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
					{

                        if (chapter % 10 == 0 || frame == 1)
                        {
                            //1 doc par 10 chapitre
                            ToonHelpers.createBundledFile(saveAs, string.Format("{0}_{1:D3}_{2:D3}", comicSavePath, chapter - 9, chapter), downloadedImages);
                            downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();
							try
							{
                                if (bitmap != null)
                                    bitmap.Dispose();
                                Directory.Delete(comicSavePath, true);
                                Directory.CreateDirectory(string.Format("{0}\\", comicSavePath));
							}
							catch (Exception){ }
                        }

                        if (frame == 1 )
                        { 
							isOk = false; 
						}
						else {
							frame = 1;
							chapter ++;

                        }


						/*
                        //pour y aller doucement
                        if (chapter == 4)
                        {
                            isOk = false;
                            //1 doc par 10 chapitre
                            ToonHelpers.createBundledFile(saveAs, string.Format("{0}_{1:D3}_{2:D3}", comicSavePath, 1, 3), downloadedImages);
                            downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();

                            Directory.Delete(comicSavePath, true);
                        }*/
                    }
				}
			}

        }
    }
}
