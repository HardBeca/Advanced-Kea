using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kea.CommonFiles
{
	class Globals
	{
		//Contains constants & variables that are shared between all classes.
		public const ushort maxSingleImageHeight = 30000;
		
		public const string episodeListHtmlXPath = "//body/div[@id='wrap']/div[@id='container']/div[@id='content']/div[@class='cont_box']/div[starts-with(@class,'detail_body')]/div[@class='detail_lst']";
		public const string episodeListItemHtmlXPath = episodeListHtmlXPath + "/ul[@id='_listUl']/li[@class='_episodeItem']";
		public const string episodeListPaginatorXPath = episodeListHtmlXPath + "/div[@class='paginate']/a";


        public const string episodeImageHtmlXPath = "//body/div[@id='wrap']/div[@id='container']/div[@id='content']/div[@class='cont_box']/div[@class='viewer_lst']/div[@id='_imageList']/img";
		
		public const string naverWebtoonAPIBaseUrl = "https://global.apis.naver.com/lineWebtoon";
		
		public const string spoofedUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_11_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/52.0.2743.116 Safari/537.36";


        public const string animeListHtmlXPath = "//body/div[@id='blocEntier']/div[@id='sousBlocMilieu']/div[@id='scansPlacement']";
        public const string animeImageListItemHtmlXPath = animeListHtmlXPath + "/img[@class='lazy lg:max-w-4xl mt-5']";

    }
}
