using HealthSys_20260428_v2.csproj.Data;
var factory = new OracleConnectionFactory();
foreach(var area in new[]{"B1","6F",""}) {
 var table=factory.GETDataTable(OracleSql.SELECT.GetDashboardExaminees(),new Dictionary<string,object?> {
 ["RESERVEDATE_START"]=new DateTime(2026,1,1),["RESERVEDATE_END"]=new DateTime(2028,1,1),
 ["ISRESERVE"]=DBNull.Value,["WORKAREA"]=area==""?DBNull.Value:area
 },"",true,30);
 Console.WriteLine("AREA="+(area==""?"ALL":area)+" COUNT="+table.Rows.Count);
}