﻿--说明：本SQL文本仅为简化SQL语句存放使用，一行只能定义一条简单语句，复杂的请单独创建文件存放。
--说明：本SQL文本仅存储下拉框所使用的语句。
--说明：注意命名规范:C_模块名_业务名

C_SYS_Role=	 SELECT RoleID AS value, RoleName AS text FROM Sys_Role
C_SYS_Department=SELECT ID AS value, DepartmentName AS text,ParentID as TopComID FROM System_Department  where DepartmentName like '%@text%'
--区域表

C_PB_Province =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 1
C_PB_City     =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 2
C_PB_District =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 3

C_PB_Province3 =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 1
C_PB_City3     =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 2
C_PB_District3 =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 3

C_PB_Province2 =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 1
C_PB_City2     =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 2
C_PB_District2 =	SELECT Code AS value,Name AS text,PCode AS parent FROM Demo_Area WHERE Lvl = 3
