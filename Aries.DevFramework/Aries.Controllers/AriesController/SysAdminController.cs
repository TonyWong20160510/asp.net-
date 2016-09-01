﻿using CYQ.Data;
using CYQ.Data.Table;
using CYQ.Data.Tool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Aries.Core;
using Aries.Logic;
using Aries.Core.Config;
using Aries.Core.Helper;
using Aries.Core.Auth;

namespace Aries.Controllers
{
    /// <summary>
    /// 重写事件
    /// </summary>
    public partial class SysAdminController : Controller
    {
        public override void Get()
        {
            switch (TableName)
            {
                case "Sys_User":
                    ObjName = "V_SYS_UserList";
                    MDataRow row = GetOne();
                    if (row != null)
                    {
                        row.Set("Password", EncrpytHelper.Decrypt(row.Get<string>("Password")));
                        jsonResult = row.ToJson();
                    }
                    break;
                default:
                    base.Get();
                    break;
            }
        }
        public override void Add()
        {
            switch (TableName)
            {
                case "Sys_User":
                    jsonResult = sysLogic.AddUser();
                    break;
                case "System_Menu":
                    jsonResult = sysLogic.AddMenu();
                    break;
                default:
                    base.Add();
                    break;
            }
        }
        public override void Update()
        {
            switch (TableName)
            {
                case "Sys_User":
                    jsonResult = sysLogic.UpdateUser();
                    break;
                default:
                    base.Update();
                    break;
            }
        }

        public override void Delete()
        {
            switch (TableName)
            {
                case "Sys_User":
                    jsonResult = sysLogic.DeleteUser();
                    break;
                default:
                    base.Delete();
                    break;
            }
        }

        protected override MDataTable Select(GridConfig.SelectType st)
        {
            MDataTable dt = base.Select(st);
            if (ObjName == "Config_ExcelInfo")
            {
                if (dt == null || dt.Rows.Count == 0)
                {
                    sysLogic.InitExcelColumn();
                    //从Excel读取列写入
                    dt = base.Select(st);
                }
            }
            return dt;
        }

        protected override void EndInvoke()
        {
            //CYQ.Data 已具备自动缓存功能，所以可以简化掉一些手工的缓存机制。
            //switch (ObjName)
            //{
            //    case "Config_KeyValue":
            //       KeyValueConfig.KeyValueTable = null;
            //        break;
            //    case "Sys_Menu":
            //        SysMenu.MenuTable = null;
            //        break;

            //}
        }
    }
    /// <summary>
    /// SysHandler 的摘要说明
    /// </summary>
    public partial class SysAdminController
    {
        SysLogic sysLogic;
        public SysAdminController()
        {
            sysLogic = new SysLogic(this);
        }
        /// <summary>
        /// 获取树菜单
        /// luoshushi
        /// </summary>
        public void GetMenu()
        {
            jsonResult = sysLogic.GetMenuJson();
            jsonResult=jsonResult.Replace(",\"ParentMenuID\":\"\"","");//兼容为空的情况。
        }

        /// <summary>
        /// 获取权限
        /// luoshushi
        /// </summary>
        public void GetActions()
        {
            jsonResult = sysLogic.GetActions();
        }
        /// <summary>
        /// 获取菜单详细数据
        /// luoshushi
        /// </summary>
        public void GetMenuDetails()
        {
            jsonResult = sysLogic.GetMenuDetails();
        }

        /// <summary>
        /// 删除菜单
        /// {id:menuid}
        /// luoshushi
        /// </summary>
        public void DeleteMenu()
        {
            jsonResult = sysLogic.DeleteMenu();
        }

        /// <summary>
        /// 验证菜单是否有子菜单
        /// </summary>
        public void ValidMenuHasChild()
        {
            jsonResult = sysLogic.ValidMenuHasChild();
        }

        public void GetMenuAndAction()
        {
            jsonResult = sysLogic.GetMenuAndAction();
        }


        public void GetMenuIDsandActionIds()
        {
            jsonResult = sysLogic.GetMenuIDsandActionIds();
        }


        public void AddPromission()
        {
            jsonResult = sysLogic.AddPromission();
        }

        public void MappingExelInfo()
        {
            jsonResult = sysLogic.GetExcelMapping();
        }



       

    }
}
