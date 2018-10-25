CREATE TABLE [Post]([ID] [nvarchar](50) NOT NULL
    ,[Title] [nvarchar](250) NOT NULL
    ,[Slug] [nvarchar](250) NOT NULL,[Excerpt] [nvarchar](max) NOT NULL
    ,[Content] [nvarchar](max) NOT NULL,[PubDate] [datetime] NULL
    ,[LastModified] [datetime] NULL,[IsPublished] [bit] NULL
    ,[IsMarkDown] [bit] NULL,[MarkDownContent] [nvarchar](max) NULL
    ,CONSTRAINT [PK_Post] PRIMARY KEY CLUSTERED ([ID] ASC));
CREATE TABLE [Categories]( [PostID] [nvarchar](50) NOT NULL, [Name] [nvarchar](250) NOT NULL
    ,CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED (  [PostID] ASC,  [Name] ASC));
CREATE TABLE [Comment](  [ID] [nvarchar](50) NOT NULL,  [Author] [nvarchar](50) NOT NULL
    ,  [Email] [nvarchar](50) NOT NULL,  [Content] [nvarchar](250) NOT NULL,  [PubDate] [datetime] NOT NULL
    ,  [IsAdmin] [bit] NOT NULL,  [PostID] [nvarchar](50) NOT NULL
    , CONSTRAINT [PK_Comment] PRIMARY KEY CLUSTERED (  [ID] ASC));