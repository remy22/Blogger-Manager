﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Blogger.v3;
using Google.Apis.Blogger.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;


namespace Blogger_Manager
{
	public partial class frmMain : Form
	{
		BloggerManager bm = new BloggerManager();
		public frmMain()
		{
			InitializeComponent();
		}

		List<Blog> _bBlogsAdmin;
		private void btnLogin_Click(object sender, EventArgs e)
		{
			_bBlogsAdmin = new List<Blog>();

			bm.Login();
			lbInfo.Items.Add("Access Token : " + bm.Credential.Token.AccessToken.ToString());
			lbInfo.Items.Add("Expires in : " + bm.Credential.Token.ExpiresInSeconds.ToString() + " s");

			int i = 1;
			foreach (Blog b in bm.listAllBlogs())
			{
				lbInfo.Items.Add("Blog #" + i .ToString() + " Name : " + b.Name);
				lbInfo.Items.Add("Blog #" + i.ToString() + " BlogId : " + b.Id);
				lbInfo.Items.Add("Blog #" + i.ToString() + " Locale :  " + b.Locale.Country + " " + b.Locale.Language + " " + b.Locale.Variant);
				lbInfo.Items.Add("Blog #" + i.ToString() +  " Pages count : " + b.Pages.TotalItems);
				lbInfo.Items.Add("Blog #" + i.ToString() + " Posts count : " + b.Posts.TotalItems);
				lbInfo.Items.Add("Blog #" + i.ToString() + " Description : " + b.Description);
				lbInfo.Items.Add("Blog #" + i.ToString() + " Blog published in  : " + XmlConvert.ToDateTime(b.Published).ToString());
				lbInfo.Items.Add("Blog #" + i.ToString() + " Blog last updated in  : " + XmlConvert.ToDateTime(b.Updated).ToString());


				BlogPerUserInfo bpui = bm.getBlogUserInfo(b.Id);
				lbInfo.Items.Add("Blog #" + i.ToString() + " User Id : " + bpui.UserId);
				lbInfo.Items.Add("Blog #" + i.ToString() + " Admin : " + bpui.HasAdminAccess);
				if (bpui.HasAdminAccess == false)
				{
					continue;
				}
				else
				{
					_bBlogsAdmin.Add(b);
					lbBlogs.Items.Add(b.Name + " ( " + b.Id + " )");
				}
				// need admin
				foreach (Pageviews.CountsData cd in bm.getPageViews(b.Id,PageViewsResource.GetRequest.RangeEnum.All))
				{
					lbInfo.Items.Add("Blog #" + i.ToString() + " Pageviews in " + cd.TimeRange + " : " + cd.Count);
				}


				i++;
				lbInfo.Items.Add(Environment.NewLine);
			}

			lbBlogs.SelectedIndex = lbBlogs.Items.Count == 0 ? -1 : 0;
		}

		private void btnLogOut_Click(object sender, EventArgs e)
		{
			bm.Logout();
		}

		private void lbBlogs_Click(object sender, EventArgs e)
		{
			(lbPosts as ListBox).Items.Clear();
			Blog b = _bBlogsAdmin[(lbBlogs as ListBox).SelectedIndex];
			List<Post> live = bm.listAllPosts(b.Id, PostsResource.ListRequest.StatusesEnum.Live);
			List<Post> draft = bm.listAllPosts(b.Id, PostsResource.ListRequest.StatusesEnum.Draft);

			live.ForEach(item => lbPosts.Items.Add(item.Title));
			draft.ForEach(item => lbPosts.Items.Add("* " + item.Title));
		}
	}

	class BloggerManager
	{

		ClientSecrets _csAppSec = new ClientSecrets
		{
			ClientId = "178155491025-hbrlr7unavrqoe3km159gjhj72bd7jrd.apps.googleusercontent.com",
			ClientSecret = "YcEEotezPs1G3n89nqucwF9M"
		};

		UserCredential _ucUser = null;
		BloggerService _bsBlog = null;
		IList<Blog> _blBlogs = null;
		FileDataStore _fdsToken = new FileDataStore("BloggerManager");

		public void Login()
		{
			//Timeout
			new Thread(delegate()
			{
				Thread.Sleep(30000);
				if (_ucUser == null)
				{
					throw new Exception("Authorization timeout 30s");
				}
			}) { IsBackground = true }.Start();

			_ucUser = GoogleWebAuthorizationBroker.AuthorizeAsync(
				_csAppSec,
				new[] { BloggerService.Scope.Blogger },
				"user",
				CancellationToken.None,
				_fdsToken
				).Result;

			_bsBlog = new BloggerService(new BaseClientService.Initializer()
				{
					HttpClientInitializer = _ucUser,
					ApplicationName = "Blogger Manager"
				});
		}

		public void Logout()
		{
			_ucUser = null;
			_bsBlog = null;
			_blBlogs = null;
			_fdsToken.ClearAsync();
		}

		public IList<Blog> listAllBlogs()
		{
			if (_bsBlog == null)
			{
				return null;
			}
			BlogsResource.ListByUserRequest req = _bsBlog.Blogs.ListByUser("self");
			BlogList bl = req.Execute();
			_blBlogs = bl.Items;
			return bl.Items;			
		}

		public IList<Pageviews.CountsData> getPageViews(string blogId, PageViewsResource.GetRequest.RangeEnum range)
		{
			if (_bsBlog == null)
			{
				return null;
			}
			PageViewsResource.GetRequest req = _bsBlog.PageViews.Get(blogId);
			req.Range = range;
			Pageviews pg = req.Execute();
			//ALL time 30 days 7 days
			return pg.Counts;
		}

		public BlogPerUserInfo getBlogUserInfo(string blogId)
		{
			if (_bsBlog == null)
			{
				return null;
			}
			BlogUserInfosResource.GetRequest req = _bsBlog.BlogUserInfos.Get("self", blogId);
			BlogUserInfo bui = req.Execute();
			return bui.BlogUserInfoValue;
		}

		public List<Post> listAllPosts(string blogId, PostsResource.ListRequest.StatusesEnum status)
		{
			PostsResource.ListRequest req = _bsBlog.Posts.List(blogId);
			req.View = PostsResource.ListRequest.ViewEnum.ADMIN;
			req.FetchBodies = false;
			req.FetchImages = false;

			req.Statuses = status;

			List<Post> listOfPost = new List<Post>();
			string firstToken = "";
			while (true)
			{
				PostList posts = req.Execute();
				req.PageToken = posts.NextPageToken;

				if (firstToken == "")
				{
					firstToken = posts.NextPageToken;
				}
				else if (firstToken != "" && posts.NextPageToken == firstToken)
				{
					break;
				}

				for (int i = 0; i < posts.Items.Count; i++)
				{
					listOfPost.Add(posts.Items[i]);
				}
			}
			return listOfPost;
			
		}


		public UserCredential Credential
		{
			get
			{
				return _ucUser;
			}
		}

		public BloggerService Service
		{
			get
			{
				return _bsBlog;
			}
		}

		public IList<Blog> BlogsList
		{
			get
			{
				return _blBlogs;
			}
		}
	}
}
