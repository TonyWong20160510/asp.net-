﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Web;
using CYQ.Data;
using CYQ.Data.Table;
using CYQ.Data.Tool;
using System.Text;
using System.IO;
using System.Web.SessionState;
using Aries.Core.Auth;
using Aries.Core.Config;
using Aries.Core.Helper;
using Aries.Core.Sql;
using Aries.Core.Extend;


namespace Aries.Core
{

    public abstract partial class Controller : IHttpHandler, IRequiresSessionState, IBase
    {
        /// <summary>
        /// 用于错误Excel的下载输出。
        /// </summary>
        private static Dictionary<string, MemoryStream> streamList = new Dictionary<string, MemoryStream>();
        /// <summary>
        /// 保存表单要修改的键值对。
        /// </summary>
        private Dictionary<string, string> keyValue = new Dictionary<string, string>();
        /// <summary>
        /// 最终被输出的Json结果
        /// </summary>
        protected string jsonResult = JsonHelper.OutResult(false, "Uninitialized");
        protected Permission p;
        protected HttpContext context;
        protected string functionName;
        public void ProcessRequest(HttpContext context)
        {
            this.context = context;
            try
            {
                functionName = Query<string>("sys_method", "").Trim(' ', ',');
                string[] items = functionName.Split(',');
                JsonHelper js = null;
                if (items.Length > 1)
                {
                    js = new JsonHelper(false, false);
                }
                foreach (string item in items)
                {
                    if (item.ToLower() == "login" || UserAuth.IsExistsToken(false))
                    {
                        if (p == null)
                        {
                            p = new Permission(UserAuth.UserName, false);
                        }
                        MethodInvoke(item);
                        if (items.Length > 1)
                        {
                            js.Add(item, jsonResult);
                        }
                        else
                        {
                            Write(jsonResult);
                        }
                    }
                    else
                    {
                        WriteError("You haven't log on yet！");
                    }
                }
                if (items.Length > 1)
                {
                    Write(js.ToString());
                }
            }
            catch (Exception err)
            {
                WriteError(err.Message);
            }

            context.Response.End();
        }
        /// <summary>
        /// 执行其他业务
        /// </summary>
        public void MethodInvoke(string functionName)
        {
            if (string.IsNullOrEmpty(functionName))
            {
                jsonResult = JsonHelper.OutResult(false, "method can't be empty");
                return;
            }
            MethodInfo method = InvokeLogic.GetMethod(this.GetType(), functionName);
            if (method == null)
            {
                jsonResult = JsonHelper.OutResult(false, "not found the method : " + functionName);
            }
            else
            {
                //权限检测
                if (p.IsCanInvokeMethod(method))
                {
                    try
                    {

                        BeforeInvoke();
                        if (!CancelInvoke)
                        {
                            object result = method.Invoke(this, null);
                            if (!CancelInvoke)
                            {
                                EndInvoke();
                            }
                            if (result != null)
                            {
                                jsonResult = Convert.ToString(result);
                            }
                        }
                    }
                    catch (System.Threading.ThreadAbortException)
                    {
                        return;
                    }
                    catch (Exception err)
                    {
                        jsonResult = JsonHelper.OutResult(false, err.InnerException.Message);
                    }
                }
                else
                {
                    SetError("Permission denied！");
                }
            }
            if (excelStream != null)
            {
                string name = OutputExcelName;
                string g = name + "#" + DateTime.Now.Ticks;
                streamList.Add(g, excelStream);
                jsonResult = "{\"sys_down\":\"" + g + "\"," + jsonResult.Substring(1);//对应的上传收到downcode会发起下载请求
            }

        }
        protected virtual void BeforeInvoke()
        {

        }
        protected virtual void EndInvoke()
        {

        }

        public bool IsReusable
        {
            get
            {
                return true;
            }
        }

        #region 基础方法

        public void SetKeyValue(Enum key, string value)
        {
            SetKeyValue(key.ToString(), value);
        }
        /// <summary>
        /// 设置表单要修改的值。
        /// </summary>
        public void SetKeyValue(string key, string value)
        {
            if (!keyValue.ContainsKey(key))
            {
                keyValue.Add(key, value);
            }
        }
        protected void SetError(string msg)
        {
            SetError(msg, null);
        }
        protected void SetSuccess(string msg)
        {
            jsonResult = JsonHelper.OutResult(true, msg);
        }
        protected void SetError(string msg, string debugInfo)
        {
#if DEBUG
            jsonResult = JsonHelper.OutResult(false, msg + debugInfo);
#else
            jsonResult = JsonHelper.OutResult(false, msg);
#endif
        }
        protected void WriteError(string msg)
        {
            Write(JsonHelper.OutResult(false, msg));
        }
        protected void Write(string result)
        {
            if (!string.IsNullOrEmpty(result))
            {
                result = result.Replace("\t", " ").Replace("\r", " ").Replace("\n", "<br/>");//Replace("\n", HttpUtility.HtmlEncode("<br/>"))
            }
            HttpContext.Current.Response.Write(result);
        }
        public string GetWhereIn(string primaryKey, string requestKey = null)
        {
            string where = string.Empty;
            if (!string.IsNullOrEmpty(requestKey))
            {
                where = Query<string>(requestKey);
            }
            else
            {
                where = GetID;
            }
            if (!string.IsNullOrEmpty(where))
            {
                string[] items = where.Split(',');
                where = string.Empty;
                foreach (var item in items)
                {
                    where += "'" + item.Trim('\'') + "',";
                }
                where = string.Format("{0} in ({1})", primaryKey, where.TrimEnd(','));//避免删除不了GUID为主键的数据
            }
            return where;
        }

        /// <summary>
        /// 构造Where条件
        /// </summary>
        protected virtual string GetWhere()
        {
            string search = Query<string>("sys_search");
            if (string.IsNullOrEmpty(search))
            {
                return search;
            }
            return SqlFormat.Format(HttpContext.Current.Server.HtmlDecode(search));
        }
        protected string GetOrderBy(string defaultSort)
        {
            string sort = Sort;
            if (string.IsNullOrEmpty(sort))
            {
                sort = defaultSort;
            }
            if (!string.IsNullOrEmpty(sort))
            {
                return string.Format(" order by {0} {1} ", sort, Order);
            }
            return "";
        }


        #endregion

        #region 属性、字段

        /// <summary>
        /// Excel文件数据流（当导入验证失败需要返回Excel文件时，可以对此属性赋值。)
        /// </summary>
        protected MemoryStream excelStream = null;
        private string _OutputExcelName;
        /// <summary>
        /// 文件数据流对应的输出文件名。
        /// </summary>
        protected string OutputExcelName
        {
            get
            {
                if (string.IsNullOrEmpty(_OutputExcelName))
                {
                    if (HttpContext.Current.Request.Files.Count > 0)
                    {
                        _OutputExcelName = HttpContext.Current.Request.Files[0].FileName.Replace(".xls", "_Error.xls");
                        _OutputExcelName = Path.GetFileName(_OutputExcelName);
                    }
                    else
                    {
                        _OutputExcelName = "Excel_Import_Error" + ".xls";
                    }
                }
                return _OutputExcelName;

            }
            set { _OutputExcelName = value; }
        }
        /// <summary>
        /// 取消继续调用事件（可以在重载BeforeInvoke方法内使用）
        /// </summary>
        protected bool CancelInvoke = false;
        public string GetID
        {
            get
            {
                string id = Query<string>("id");
                if (string.IsNullOrEmpty(id) && HttpContext.Current.Request.QueryString.Keys.Count > 0)
                {
                    for (int i = 0; i < HttpContext.Current.Request.QueryString.Keys.Count; i++)
                    {
                        if (HttpContext.Current.Request.QueryString.Keys[i].ToLower().EndsWith("id"))
                        {
                            return Query<string>(HttpContext.Current.Request.QueryString.Keys[i], string.Empty);
                        }
                    }
                }
                return id;
            }
        }


        /// <summary>
        /// 对名对称对应的SQL语句
        /// </summary>
        public string ObjCode
        {
            get
            {

                return SqlCode.GetCode(ObjName);
            }
        }
        /// <summary>
        /// 跨库对象名称
        /// </summary>
        public object CrossObjName
        {
            get
            {
                return CrossDb.GetEnum(ObjCode);
            }
        }
        private string _ObjName;
        /// <summary>
        /// 远程传过来的对象名称
        /// </summary>
        public string ObjName
        {
            get
            {
                if (string.IsNullOrEmpty(_ObjName))
                {
                    _ObjName = Query<string>("sys_objName");
                    if (string.IsNullOrEmpty(_ObjName) || _ObjName.Contains(" "))
                    {
                        WriteError("ObjName can't be empty or contain blank!");
                    }
                    return _ObjName;
                }
                return _ObjName;
            }
            set
            {
                _ObjName = value;
            }
        }
        private string _TableName;
        /// <summary>
        /// 对象视图名称的主表名
        /// </summary>
        public string TableName
        {
            get
            {
                if (string.IsNullOrEmpty(_TableName))
                {
                    _TableName = Query<string>("sys_tableName");
                    if (string.IsNullOrEmpty(_TableName))
                    {
                        _TableName = ObjName;
                    }
                    else if (_TableName.Contains(" "))
                    {
                        WriteError("TableName can't contain blank!");
                    }

                    return _TableName;
                }
                return _TableName;
            }
        }

        #endregion

        #region 增删改查
        /// <summary>
        /// 允许重写导出的数据源（适用于存储过程的自定义数据源）
        /// </summary>
        protected virtual MDataTable Select(GridConfig.SelectType st)
        {
            MDataTable dt;
            using (MAction action = new MAction(CrossObjName))
            {
                action.SetSelectColumns(GridConfig.GetSelectColumns(ObjName, st));//只查询要显示的列数据。
                dt = action.Select(PageIndex, PageSize, GetWhere() + GetOrderBy(action.Data.PrimaryCell.ColumnName));
            }
            return dt;
        }
        [ActionKey("View,Get")]
        /// <summary>
        /// 读取界面列表数据
        /// </summary>
        public virtual void GetList()
        {
            jsonResult = Select(GridConfig.SelectType.Show).ToJson();
        }
        /// <summary>
        /// 获取一行数据。
        /// </summary>
        protected virtual MDataRow GetOne()
        {
            using (MAction action = new MAction(CrossObjName))
            {
                string where = string.IsNullOrWhiteSpace(GetID) ? GetWhere() : GetID.ToString();
                if (action.Fill(where))
                {
                    return action.Data;
                }
            }
            return null;
        }
        [ActionKey("View,Get")]
        /// <summary>
        /// 获取一条数据
        /// </summary>
        public virtual void Get()
        {
            MDataRow row = GetOne();
            if (row != null)
            {
                jsonResult = row.ToJson();
            }
        }
        /// <summary>
        /// 添加一条数据
        /// </summary>
        [ActionKey("Add,Insert")]
        public virtual void Add()
        {
            bool result = false;
            string msg = string.Empty;
            using (MAction action = new MAction(CrossObjName))
            {
                SetKeyValue(action.Data);
                result = action.Insert(true, InsertOp.ID);
                if (result)
                {
                    msg = action.Get<string>(action.Data.PrimaryCell.ColumnName);
                    SetSuccess(msg);
                }
                else
                {
                    if (AppConfig.Debug.OpenDebugInfo)
                    {
                        Log.WriteLogToTxt("Add(): " + action.DebugInfo);
                    }
                    SetError("添加失败！", action.DebugInfo);
                }
            }
        }
        /// <summary>
        /// 在进行新增加前处理的事件（action参数不需要关闭）。
        /// 数据行直接拿action.Data
        /// 返回true则继续执行更新，返回false则停止更新。
        /// </summary>
        private void SetKeyValue(MDataRow row)
        {
            if (keyValue.Count > 0)
            {
                foreach (KeyValuePair<string, string> item in keyValue)
                {
                    row.Set(item.Key, item.Value);
                }
            }
        }
        /// <summary>
        /// 删除数据（传ID则一条，也可以构造where条件删除）
        /// </summary>
        [ActionKey("Del,Delete")]
        public virtual void Delete()
        {

            using (MAction action = new MAction(CrossObjName))
            {
                var primaryKey = action.Data.PrimaryCell.ColumnName;
                string where = GetID;
                if (!string.IsNullOrEmpty(where))
                {
                    where = GetWhereIn(primaryKey);
                }
                else
                {
                    where = GetWhere();
                }

                if (!string.IsNullOrEmpty(where) && action.Delete(where))
                {
                    SetSuccess("删除成功！");
                }
                else
                {
                    if (AppConfig.Debug.OpenDebugInfo)
                    {
                        Log.WriteLogToTxt("Delete(): " + action.DebugInfo);
                    }
                    SetError("删除失败！", action.DebugInfo);
                }
            }
        }
        /// <summary>
        /// 更新一条数据
        /// </summary>
        [ActionKey("Edit,Update")]
        public virtual void Update()
        {
            using (MAction action = new MAction(CrossObjName))
            {
                SetKeyValue(action.Data);
                if (action.Update(true))
                {
                    SetSuccess("更新成功！");
                }
                else
                {
                    if (AppConfig.Debug.OpenDebugInfo)
                    {
                        Log.WriteLogToTxt("Update(): " + action.DebugInfo);
                    }
                    SetError("更新失败！", action.DebugInfo);
                }
            }

        }


        #endregion

        #region 导入导出
        /// <summary>
        /// 导出数据
        /// </summary>
        [ActionKey("Export")]
        public void Export()
        {
            MDataTable dt = Select(GridConfig.SelectType.Export);
            dt.TableName = ObjName;
            dt.DynamicData = GridConfig.GetList(ObjName, GridConfig.SelectType.All);
            Dictionary<string, string> formatParas = GridConfig.SetHeaderDescription(dt); //格式化列头（描述）（需要修改列头的数据格式）
            KeyValueConfig.FormatNameValue(dt, formatParas, true); //格式化配置项
            WebHelper.SendFile(KeyValueConfig.GetTableDescription(ObjName, TableName) + "_" + DateTime.Now.ToString("yyyyMMdd") + ".xls", ExcelHelper.CreateExcel(dt));//ExcelHelper.CreateExcel(dt)
        }

        /// <summary>
        /// 获取导入数据的模板（支持2007以上）
        /// </summary>
        public void ExcelTemplate()
        {
            string path = HttpContext.Current.Server.MapPath("~/Resource/Excel/" + ObjName + ".xls");
            if (!File.Exists(path))
            {
                path = path + "x";
                if (!File.Exists(path))
                {
                    path = HttpContext.Current.Server.MapPath("~/Resource/Excel/" + TableName + ".xls");
                    if (!File.Exists(path))
                    {
                        path = path + "x";
                    }
                }
            }

            MemoryStream ms = null;

            if (File.Exists(path))
            {
                byte[] data = File.ReadAllBytes(path);
                ms = new MemoryStream(data, 0, data.Length, false, true);
            }
            string fix = "_模板";
            if (ms == null)
            {
                fix = "-模板";
                string objName = ObjName;
                MDataTable dt = GridConfig.GetList(objName, GridConfig.SelectType.All);//获取所有列的字段名。
                if (dt.Rows.Count > 0)
                {
                    ms = ExcelHelper.CreateExcelHeader(dt, KeyValueConfig.GetValidationData(dt));
                }
            }
            WebHelper.SendFile(KeyValueConfig.GetTableDescription(ObjName, TableName) + fix + Path.GetExtension(path), ms);
        }
        /// <summary>
        /// 下载错误的Excel列表。
        /// </summary>
        public void Down()
        {
            string downCode = Query<string>("sys_down");
            if (!string.IsNullOrEmpty(downCode) && streamList.ContainsKey(downCode))
            {
                MemoryStream ms = streamList[downCode];
                try
                {
                    streamList.Remove(downCode);
                }
                catch
                {

                }
                string name = downCode.Split('#')[0];
                WebHelper.SendFile(name, ms);
            }
        }

        /// <summary>
        /// 导入数据
        /// </summary>
        [ActionKey("Import")]
        public void Import()
        {
            //根据视图名读取ExcelConfig信息。

            MDataRow excelInfo = null;
            int index = 0, headCrossRowNum = 0;
            string sheetName = null;
            try
            {
                excelInfo = ExcelConfig.GetExcelRow(ObjName);
                if (excelInfo != null)
                {
                    index = excelInfo.Get<int>("StartIndex", 0);
                    headCrossRowNum = excelInfo.Get<int>("HeadCrossRowNum", 0);
                    sheetName = excelInfo.Get<string>("CnName");
                }
            }
            catch (Exception err)
            {
                Log.WriteLogToTxt(err);//避免其它地方没有升级数据库表脚本。
            }
            MDataTable dt = ExcelHelper.ReadExcel(excelInfo != null, null, index, headCrossRowNum, sheetName);
            if (!dt.Columns.Contains("错误信息"))
            {
                dt.Columns.Add("错误信息", System.Data.SqlDbType.NVarChar);
            }
            dt.TableName = excelInfo != null ? ObjName : TableName;
            bool result = false;
            string msg = string.Empty;
            ImportResult iResult = BeforeImport(dt, excelInfo, out msg);
            if (iResult == ImportResult.Continue)
            {
                result = FormatExcel(dt, excelInfo);
                if (result)
                {
                    result = ExcelConfig.AcceptChanges(dt, excelInfo, ObjName);// dt.AcceptChanges(AcceptOp.Auto);
                }
            }
            else
            {
                result = iResult == ImportResult.True;
            }
            if (!result)
            {
                if (dt.DynamicData != null && dt.DynamicData is Exception)
                {
                    msg = ((Exception)dt.DynamicData).Message;
                    msg += "（PS：可能模板不匹配）";
                }
                if (excelStream == null)
                {
                    excelStream = ExcelHelper.SetError(dt);
                }
                if (string.IsNullOrEmpty(msg))
                {
                    msg = "导入失败" + (excelStream != null ? "(请查看输出的Excel错误信息)" : "（请检测是否模板错误）");
                }
            }
            else if (string.IsNullOrEmpty(msg))
            {
                msg = "导入成功";
            }
            dt.DynamicData = null;
            dt = null;
            GC.Collect();

            jsonResult = JsonHelper.OutResult(result, msg);
        }
        protected bool FormatExcel(MDataTable dt)
        {
            return FormatExcel(dt, null);
        }
        /// <summary>
        /// 格式化Excel列头和Config_KeyValue数据。
        /// </summary>
        protected bool FormatExcel(MDataTable dt, MDataRow excelInfo)
        {

            //翻译列头。
            Dictionary<string, string> formatterDic = ExcelConfig.FormatterTitle(dt, excelInfo, ObjName);//
            //翻译字典值，处理默认值。
            bool result = KeyValueConfig.FormatNameValue(dt, formatterDic, false);

            //校验基础数据类型。
            result = ExcelConfig.ValidateData(dt, excelInfo) && result;

            if (result)//验证是否存在有错误信息
            {
                int index = dt.Columns.GetIndex("错误信息");
                if (index > -1)
                {
                    foreach (var row in dt.Rows)
                    {
                        if (!row[index].IsNullOrEmpty)
                        {
                            result = false;
                            break;
                        }
                    }
                }
            }
            dt.SetState(2, BreakOp.NullOrEmpty);
            return result;
        }
        /// <summary>
        /// 可以对导入的数据进行验证
        /// </summary>
        public virtual ImportResult BeforeImport(MDataTable dt, MDataRow excelInfo, out string msg)
        {
            msg = "";
            return ImportResult.Continue;
        }
        #endregion

        #region 初始数据获取
        /// <summary>
        /// 获取UI、MID、ActionKey的配置值。
        /// </summary>
        public void GetInitConfig()
        {
            string ui = string.Empty, actionKeys = string.Empty, menuID = string.Empty;
            if (IsUseUISite)
            {
                ui = "/" + AppConfig.GetApp("UI").Trim('/');
            }
            actionKeys = p.GetFuncKeys();
            if (!string.IsNullOrEmpty(actionKeys))
            {
                actionKeys = "," + actionKeys.ToLower() + ",";
            }

            menuID = p.UrlMenuID;
            JsonHelper js = new JsonHelper(false, false);
            js.Add("ui", ui);
            js.Add("actionKeys", actionKeys);
            js.Add("mid", menuID);
            jsonResult = js.ToString();
        }
        /// <summary>
        /// 获取Config_KeyValue的配置值。
        /// </summary>
        public void GetKeyValueConfig()
        {
            jsonResult = KeyValueConfig.GetJson();
        }
        /// <summary>
        /// 获取表的列头数据（输出EasyUI的列头格式）
        /// </summary>
        public void GetHeader()
        {
            MDataTable dt = GridConfig.GetList(ObjName, GridConfig.SelectType.All);
            if (dt == null || dt.Rows.Count == 0)
            {
                dt = GridConfig.Create(ObjCode, dt.GetSchema(false));
            }
            else
            {
                //dt = GridConfig.Check(ObjCode, dt);
            }
            //顺带处理视图语句与菜单名称的绑定
            KeyValueConfig.SetTableDescription(ObjName, p.MenuName);
            jsonResult = dt.ToJson(false, false, true);
        }
        /// <summary>
        /// 下拉框统一处理参数对象
        /// </summary>
        internal class ComboboxItem
        {
            public string ObjName { get; set; }
            public string Parent { get; set; }
        }
        //[ActionKey("View")]
        /// <summary>
        /// 获取下拉框Json结果集(统一处理）
        /// </summary>
        /// <returns></returns>
        public void GetCombobox()
        {
            string itemJson = Query<string>("sys_json");
            if (!string.IsNullOrEmpty(itemJson))
            {
                List<ComboboxItem> boxes = JsonHelper.ToList<ComboboxItem>(itemJson);
                JsonHelper json = new JsonHelper();
                if (boxes.Count > 0)
                {
                    List<MDataTable> dtList = new List<MDataTable>();
                    StringBuilder sb = new StringBuilder();
                    string value = null;
                    for (int i = 0; i < boxes.Count; i++)
                    {
                        ComboboxItem item = boxes[i];
                        string code = SqlCode.GetCode(item.ObjName);
                        if (code != item.ObjName)
                        {
                            #region 核心处理
                            var sql = SqlCode.GetCode(item.ObjName).ToLower();
                            //格式化请求参数
                            string key = "@text";
                            int index = sql.IndexOf(key);
                            if (index > -1)
                            {
                                value = Query<string>("q");//easyui远程传递参数
                                if (string.IsNullOrEmpty(value) && sql.IndexOf('=', index - 5, 5) > -1)//处理成1=1，同时有=号
                                {
                                    int end = index + key.Length;
                                    string temp = sql.Substring(0, index - 5);
                                    int start = temp.LastIndexOf(' ');
                                    sql = sql.Replace(sql.Substring(start + 1, end - start - 1), "1=1");
                                }
                                else
                                {
                                    sql = sql.Replace(key, value);
                                }
                            }
                            sb.Append(sql + ";");
                            #endregion
                        }
                        else
                        {
                            #region 找不到，则移除。
                            boxes.RemoveAt(i);
                            //从程序里找。
                            MDataTable dt = Combobox.Get(item.ObjName, Query<string>("q"));
                            if (dt != null)
                            {
                                dtList.Insert(0, dt);
                                boxes.Insert(0, item);
                            }
                            else
                            {
                                i--;
                            }
                            #endregion
                        }
                    }
                    if (sb.Length > 0)
                    {
                        string sql = sb.ToString().TrimEnd(';');
                        using (MProc proc = new MProc(sql, CrossDb.GetConn(sql)))
                        {
                            if (proc.DalType == DalType.MsSql)
                            {
                                dtList.AddRange(proc.ExeMDataTableList());
                            }
                            else
                            {
                                string[] items = sql.Split(';');
                                foreach (string item in items)
                                {
                                    proc.ResetProc(item);
                                    dtList.Add(proc.ExeMDataTable());
                                }
                            }
                        }
                    }
                    if (dtList.Count == 1 && Query<string>("q", null) != null)
                    {
                        jsonResult = dtList[0].ToJson(false, false, true);
                        return;
                    }
                    for (int i = 0; i < dtList.Count; i++)
                    {
                        json.Add(boxes[i].ObjName, dtList[i].Rows.Count > 0 ? dtList[i].ToJson(false, false, true) : "[]", true);
                        if (!string.IsNullOrEmpty(boxes[i].Parent))
                        {
                            json.Add("Target", boxes[i].Parent);
                        }
                        json.AddBr();
                    }

                }
                jsonResult = string.IsNullOrEmpty(json.ToString()) ? "[]" : json.ToString(true);
            }
        }

        [ActionKey("View")]
        /// <summary>
        /// 获取框架文件对应的SQL语句 By CYQ
        /// </summary>
        public void GetSQL()
        {
            string sql = SqlCode.GetSourceCode(ObjName);
            bool result = !string.IsNullOrEmpty(sql);
            jsonResult = JsonHelper.OutResult(result, sql);
        }
        [ActionKey("SaveSQL")]
        /// <summary>
        /// 保存框架文件对应的SQL语句 By CYQ
        /// </summary>
        public void SaveSQL()
        {
            string msg;
            bool result = SqlCode.SaveSourceCode(ObjName, Query<string>("sys_code"), out msg);
            jsonResult = JsonHelper.OutResult(result, result ? "保存成功！" : "保存失败!" + msg);
        }
        [ActionKey("View")]
        /// <summary>
        /// 获取Config_Grid的某配置项的脚本
        /// </summary>
        public void GetGridConfigScript()
        {
            string script = SqlScript.GetGridConfigScript(ObjName);
            bool result = !string.IsNullOrEmpty(script);
            if (result)
            {
                WebHelper.SendFile("sys_Grid配置_" + KeyValueConfig.GetTableDescription(ObjName, TableName) + ".sql", script);
            }
            else
            {
                jsonResult = JsonHelper.OutResult(result, script);
            }
        }
        [ActionKey("View")]
        /// <summary>
        /// 获取Config_ExcelInfo的某配置项的脚本
        /// </summary>
        public void GetExcelConfigScript()
        {
            string script = SqlScript.GetExcelConfigScript(GetID);
            bool result = !string.IsNullOrEmpty(script);
            if (result)
            {
                WebHelper.SendFile("sys_Excel配置_" + KeyValueConfig.GetTableDescription(ObjName, TableName) + ".sql", script);
            }
            else
            {
                jsonResult = JsonHelper.OutResult(result, script);
            }
        }
        /// <summary>
        /// 验证字段重复
        /// </summary>
        public void ValidFieldRepeat()
        {
            string value = Query<string>("v", "");
            string name = Query<string>("n", "");
            string op = Query<string>("op", "");
            bool result = false;
            using (MAction action = new MAction(CrossObjName))
            {
                if (op.ToLower() == "add")
                {
                    result = action.GetCount(string.Format("{1}='{0}'", value, name)) == 0;
                }
                else if (op.ToLower() == "update")
                {
                    var primarykey = action.Data.PrimaryCell.ColumnName;
                    result = action.GetCount(string.Format("{1}='{0}' AND {2} <> '{3}'", value, name, primarykey, GetID)) == 0;
                }
            }
            jsonResult = JsonHelper.OutResult(result, string.Empty);
        }
        #endregion

        /// <summary>
        /// 是否使用子应用程序部署
        /// </summary>
        public bool IsUseUISite
        {
            get { return WebHelper.IsUseUISite; }
        }
    }

    public abstract partial class Controller
    {
        /// <summary>
        /// datagrid分页的页码数
        /// </summary>
        public int PageIndex
        {
            get
            {
                return Query<int>("page");
            }
        }
        /// <summary>
        /// datagrid分页的页容量
        /// </summary>
        public int PageSize
        {
            get
            {
                return Query<int>("rows");
            }
        }

        /// <summary>
        /// 排序字段名
        /// </summary>
        public string Sort
        {
            get
            {
                return Query<string>("sort", "");
            }

        }
        /// <summary>
        /// 排序类型（升或降）
        /// </summary>
        public string Order
        {
            get
            {
                return Query<string>("order", "desc");
            }
        }
        public T Query<T>(Enum key)
        {
            return Query<T>(key.ToString(), default(T));
        }
        public T Query<T>(string key)
        {
            return Query<T>(key, default(T));
        }
        public T Query<T>(string key, T defaultValue)
        {
            return WebHelper.Query<T>(key, defaultValue, false);
        }


        public HttpContext Context
        {
            get { return HttpContext.Current; }
        }


        public bool IsHttpGet
        {
            get { return Context.Request.RequestType == "GET"; }
        }

        public bool IsHttpPost
        {
            get { return Context.Request.RequestType == "POST"; }
        }
    }
    /// <summary>
    /// BeForeImport的结果参数
    /// </summary>
    public enum ImportResult
    {
        /// <summary>
        /// 继续执行原有流程。
        /// </summary>
        Continue,
        /// <summary>
        /// 中止执行，并返回True结果
        /// </summary>
        True,
        /// <summary>
        ///  中止执行，并返回False结果
        /// </summary>
        False
    }

}
