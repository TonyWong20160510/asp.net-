/* 
��д��Ա��XXX 
��д���ڣ�2014-04-16 
 
�����޸ģ��ޣ����ڣ� 
 
����˵�����û���ͼ ------Demo���á� 
ʹ�� URL��/Web/Module/Systems/Ty_UserList.aspx 
where a.CompanyID=@CompanyID 
*/ 
SELECT u.*,ui.* FROM Sys_User u left join Sys_UserInfo ui on u.UserID=ui.UserInfoID