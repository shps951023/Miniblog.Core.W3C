CREATE TABLE Post(ID nvarchar(50) NOT NULL
    ,Title nvarchar(250) NOT NULL
    ,Slug nvarchar(250) NOT NULL,Excerpt text NOT NULL
    ,Content text NOT NULL,PubDate datetime NULL
    ,LastModified datetime NULL,IsPublished bit NULL
    ,IsMarkDown bit NULL,MarkDownContent text NULL);
CREATE TABLE Categories( PostID nvarchar(50) NOT NULL, Name nvarchar(250) NOT NULL);
CREATE TABLE Comment(  ID nvarchar(50) NOT NULL,  Author nvarchar(50) NOT NULL
    ,  Email nvarchar(50) NOT NULL,  Content nvarchar(250) NOT NULL,  PubDate datetime NOT NULL
    ,  IsAdmin bit NOT NULL,  PostID nvarchar(50) NOT NULL);