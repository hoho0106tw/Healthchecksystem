
using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace HealthSys_20260428_v2.csproj.Data;

public static class OracleSql
{
    public static class UserRole
    {
        public const string RoleFilterOptions = @"SELECT ROLE_ID, ROLE_NAME FROM ROLE_MASTER ORDER BY ROLE_ID";
        public const string UpdateStaff = @"UPDATE ACCOUNT SET ROLE_CODE = :ROLE_CODE, STATUS = :STATUS WHERE H_EMPNO = :EMP_NO";
        public const string DeleteStaff = @"DELETE FROM ACCOUNT WHERE H_EMPNO = :EMP_NO";
        public const string Roles = @"SELECT ROLE_ID, ROLE_NAME, ROLE_DESC FROM ROLE_MASTER WHERE IS_ACTIVE = 1 ORDER BY CDATE, ROLE_ID";
        public const string ActiveRole = @"SELECT ROLE_ID FROM ROLE_MASTER WHERE ROLE_ID = :ROLE_ID AND IS_ACTIVE = 1";
        public const string Permissions = @"SELECT F.FUNCTION_ID, F.PARENT_ID, F.FUNCTION_NAME, F.SORT_NO,
NVL(P.CAN_VIEW, 0) CAN_VIEW, NVL(P.CAN_REVIEW, 0) CAN_REVIEW
FROM SYSTEM_FUNCTION F LEFT JOIN ROLE_PERMISSION P ON P.FUNCTION_ID = F.FUNCTION_ID AND P.ROLE_ID = :ROLE_ID
WHERE F.IS_ACTIVE = 1 ORDER BY F.SORT_NO, F.FUNCTION_ID";
        public const string MergePermission = @"MERGE INTO ROLE_PERMISSION P
USING (SELECT :ROLE_ID ROLE_ID, :FUNCTION_ID FUNCTION_ID, :CAN_VIEW CAN_VIEW, :CAN_REVIEW CAN_REVIEW FROM DUAL) S
ON (P.ROLE_ID = S.ROLE_ID AND P.FUNCTION_ID = S.FUNCTION_ID)
WHEN MATCHED THEN UPDATE SET P.CAN_VIEW = S.CAN_VIEW, P.CAN_REVIEW = S.CAN_REVIEW, P.UPDATE_USER = :UPDATE_USER, P.UPDATE_DATE = SYSDATE
WHEN NOT MATCHED THEN INSERT (ROLE_ID, FUNCTION_ID, CAN_VIEW, CAN_REVIEW, UPDATE_USER, UPDATE_DATE)
VALUES (S.ROLE_ID, S.FUNCTION_ID, S.CAN_VIEW, S.CAN_REVIEW, :UPDATE_USER, SYSDATE)";
        public const string InsertRole = @"INSERT INTO ROLE_MASTER (ROLE_ID, ROLE_NAME, ROLE_DESC, IS_ACTIVE, CDATE, UPDATE_DATE)
VALUES (:ROLE_ID, :ROLE_NAME, :ROLE_DESC, 1, SYSDATE, SYSDATE)";
        public const string InitializePermissions = @"INSERT INTO ROLE_PERMISSION (ROLE_ID, FUNCTION_ID, CAN_VIEW, CAN_REVIEW, UPDATE_USER, UPDATE_DATE)
SELECT :ROLE_ID, FUNCTION_ID, 0, 0, :UPDATE_USER, SYSDATE FROM SYSTEM_FUNCTION";
        // Oracle evaluates the right-hand expressions using the original ROLE_NAME.
        // Synchronize unchanged descriptions; preserve text explicitly edited or already synchronized by the form.
        public const string UpdateRole = @"UPDATE ROLE_MASTER SET ROLE_NAME = :ROLE_NAME,
ROLE_DESC = CASE WHEN ROLE_NAME <> :ROLE_NAME
    AND (TRIM(:ROLE_DESC) IS NULL OR :ROLE_DESC = ROLE_DESC) THEN
    CASE WHEN TRIM(:ROLE_DESC) IS NULL THEN :ROLE_NAME ELSE REPLACE(:ROLE_DESC, ROLE_NAME, :ROLE_NAME) END
    ELSE :ROLE_DESC END,
IS_ACTIVE = :IS_ACTIVE, UPDATE_DATE = SYSDATE WHERE ROLE_ID = :ROLE_ID AND IS_ACTIVE = 1";
        public const string AuditRoleChange = @"UPDATE ROLE_PERMISSION SET UPDATE_USER = :UPDATE_USER, UPDATE_DATE = SYSDATE WHERE ROLE_ID = :ROLE_ID";
        // Lock the active role in the same transaction as permission writes; a concurrent disable must not be bypassed.
        public const string LockRole = @"UPDATE ROLE_MASTER SET UPDATE_DATE = SYSDATE WHERE ROLE_ID = :ROLE_ID AND IS_ACTIVE = 1";
    }

    #region SELECT - 查詢語法函式

    public static class SELECT
    {
        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildHospitalMedicalOrdersQuery(
            string orderCode, string orderName, string initial = "")
        {
            var filters = new List<string> { "CHORDCOMP NOT IN ('1', '2')" };
            var parameters = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(orderCode))
            {
                filters.Add("INSTR(UPPER(CHORDNO), UPPER(:ORDER_CODE)) > 0");
                parameters["ORDER_CODE"] = orderCode.Trim();
            }
            if (!string.IsNullOrWhiteSpace(orderName))
            {
                filters.Add("INSTR(UPPER(CHORDENAME), UPPER(:ORDER_NAME)) > 0");
                parameters["ORDER_NAME"] = orderName.Trim();
            }

            var normalizedInitial = initial.Trim().ToUpperInvariant();
            if (normalizedInitial == "0-9")
            {
                filters.Add("REGEXP_LIKE(TRIM(CHORDNO), '^[0-9]')");
            }
            else if (normalizedInitial == "OTHER")
            {
                filters.Add("(TRIM(CHORDNO) IS NULL OR NOT REGEXP_LIKE(UPPER(TRIM(CHORDNO)), '^[0-9A-Z]', 'c'))");
            }
            else if (normalizedInitial.Length == 1 && normalizedInitial[0] >= 'A' && normalizedInitial[0] <= 'Z')
            {
                filters.Add("SUBSTR(UPPER(TRIM(CHORDNO)), 1, 1) = :ORDER_INITIAL");
                parameters["ORDER_INITIAL"] = normalizedInitial;
            }

            return ($"SELECT DISTINCT CHORDNO, CHORDENAME, CHORDUNIT FROM GenOrdBasictbl WHERE {string.Join(" AND ", filters)}", parameters);
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildHospitalMedicalOrdersPageQuery(
            string orderCode, string orderName, string initial, int page, int pageSize)
        {
            var query = BuildHospitalMedicalOrdersQuery(orderCode, orderName, initial);
            var parameters = query.Parameters.ToDictionary(pair => pair.Key, pair => pair.Value);
            parameters["REQUESTED_PAGE"] = Math.Max(1, page);
            parameters["PAGE_SIZE"] = new[] { 10, 20, 30, 50 }.Contains(pageSize) ? pageSize : 20;
            return ($@"WITH ranked_orders AS (
    SELECT matched_orders.*,
           COUNT(*) OVER () AS TOTAL_COUNT,
           ROW_NUMBER() OVER (ORDER BY CHORDNO, CHORDENAME, CHORDUNIT) AS ROW_NO
    FROM ({query.Sql}) matched_orders
), page_orders AS (
    SELECT ranked_orders.*,
           LEAST(:REQUESTED_PAGE, CEIL(TOTAL_COUNT / :PAGE_SIZE)) AS RESULT_PAGE
    FROM ranked_orders
)
SELECT CHORDNO, CHORDENAME, CHORDUNIT, TOTAL_COUNT, RESULT_PAGE
FROM page_orders
WHERE ROW_NO BETWEEN (RESULT_PAGE - 1) * :PAGE_SIZE + 1 AND RESULT_PAGE * :PAGE_SIZE
ORDER BY ROW_NO", parameters);
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionImageRequestNumbersQuery(
            string chop1Date,
            string chop1Time,
            string chop1Room,
            int intop1No)
        {
            const string sql = @"SELECT DISTINCT
    CHOP4GREQNO,
    CHOP4REQNO
FROM OpdOrdTbl
WHERE chOp1Date = :CHOP1DATE
  AND chOp1Time = :CHOP1TIME
  AND chOp1Room = :CHOP1ROOM
  AND intOp1No = :INTOP1NO
  AND chOp4Stat <> 'DC'
  AND CHOP4REQTYPE = 'R'
  AND CHOP4GREQNO IS NOT NULL
  AND CHOP4REQNO IS NOT NULL";

            return (sql, new Dictionary<string, object?>
            {
                ["CHOP1DATE"] = OracleParameterValue.Char(chop1Date, 7),
                ["CHOP1TIME"] = OracleParameterValue.Char(chop1Time, 1),
                ["CHOP1ROOM"] = OracleParameterValue.Char(chop1Room, 6),
                ["INTOP1NO"] = OracleParameterValue.Number(intop1No)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionSpecialRequestNumbersQuery(
            string chop1Date,
            string chop1Time,
            string chop1Room,
            int intop1No)
        {
            const string sql = @"SELECT DISTINCT
    CHOP4GREQNO,
    CHOP4REQNO
FROM OpdOrdTbl
WHERE chOp1Date = :CHOP1DATE
  AND chOp1Time = :CHOP1TIME
  AND chOp1Room = :CHOP1ROOM
  AND intOp1No = :INTOP1NO
  AND chOp4Stat <> 'DC'
  AND CHOP4REQTYPE = 'S'
  AND CHOP4GREQNO IS NOT NULL
  AND CHOP4REQNO IS NOT NULL";

            return (sql, new Dictionary<string, object?>
            {
                ["CHOP1DATE"] = OracleParameterValue.Char(chop1Date, 7),
                ["CHOP1TIME"] = OracleParameterValue.Char(chop1Time, 1),
                ["CHOP1ROOM"] = OracleParameterValue.Char(chop1Room, 6),
                ["INTOP1NO"] = OracleParameterValue.Number(intop1No)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionEcgRequestNumbersQuery(
            string chop1Date,
            string chop1Time,
            string chop1Room,
            int intop1No)
        {
            const string sql = @"SELECT CHOP4GREQNO, CHOP4REQNO
FROM OpdOrdTbl
WHERE CHOP1DATE = :CHOP1DATE
  AND CHOP1TIME = :CHOP1TIME
  AND CHOP1ROOM = :CHOP1ROOM
  AND INTOP1NO = :INTOP1NO
  AND CHOP4STAT <> 'DC'
  AND CHOP4ORDNO = 'K001'
ORDER BY INTOP1ORDSEQ";

            return (sql, new Dictionary<string, object?>
            {
                ["CHOP1DATE"] = OracleParameterValue.Char(chop1Date, 7),
                ["CHOP1TIME"] = OracleParameterValue.Char(chop1Time, 1),
                ["CHOP1ROOM"] = OracleParameterValue.Char(chop1Room, 6),
                ["INTOP1NO"] = OracleParameterValue.Number(intop1No)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionEcgReportsQuery(
            string groupRequestNumber,
            string requestNumber)
        {
            var vchGroupRequestNumber = groupRequestNumber.Trim();
            var vchRequestNumber = requestNumber.Trim();
            const string sql = @"SELECT AUTOSTATEMENT_NEW, REPORTSTATUS, LASTMODIFYDATE, OWNERUSERID
FROM EKGReportTbl
WHERE VCHGREQNO = :VCHGREQNO
  AND VCHREQNO = :VCHREQNO
  AND REPORTSTATUS = '2'";

            return (sql, new Dictionary<string, object?>
            {
                ["VCHGREQNO"] = OracleParameterValue.Varchar2(vchGroupRequestNumber, vchGroupRequestNumber.Length),
                ["VCHREQNO"] = OracleParameterValue.Varchar2(vchRequestNumber, vchRequestNumber.Length)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionSpecialReportsQuery(
            string groupRequestNumber,
            string requestNumber)
        {
            const string sql = @"SELECT rslt.CHSTAT, rslt.CHMODNM, rslt.CHMODDTM,
    CASE TRIM(rslt.CHSTAT)
        WHEN '10' THEN '申請中'
        WHEN '30' THEN '已簽收'
        WHEN '50' THEN '部份報告'
        WHEN '60' THEN '報告確認'
        WHEN '70' THEN '報告複審'
        ELSE '其他'
    END AS CHSTAT_NAME
FROM ADMRQTRCPSWTBL rslt
WHERE rslt.CHGREQNO = :CHGREQNO AND rslt.CHREQNO = :CHREQNO
  AND TRIM(rslt.CHSTAT) IN ('10', '30', '50', '60', '70')
ORDER BY TRIM(rslt.CHSTAT) DESC NULLS LAST,
         rslt.CHMODDTM DESC NULLS LAST";

            return (sql, new Dictionary<string, object?>
            {
                ["CHGREQNO"] = OracleParameterValue.Char(groupRequestNumber.Trim(), groupRequestNumber.Trim().Length),
                ["CHREQNO"] = OracleParameterValue.Char(requestNumber.Trim(), requestNumber.Trim().Length)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionSpecialReportTextQuery(
            string groupRequestNumber,
            string requestNumber)
        {
            const string sql = @"SELECT CHTXT
FROM ADMRESULTTEXTSLISTBL
WHERE CHGREQNO = :CHGREQNO AND CHREQNO = :CHREQNO";

            return (sql, new Dictionary<string, object?>
            {
                ["CHGREQNO"] = OracleParameterValue.Char(groupRequestNumber.Trim(), groupRequestNumber.Trim().Length),
                ["CHREQNO"] = OracleParameterValue.Char(requestNumber.Trim(), requestNumber.Trim().Length)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionImageReportsQuery(
            string hisGroupRequestNumber,
            string hisRequestNumber)
        {
            const string sql = @"SELECT
    A.HISGREQNO,
    A.HISREQNO,
    A.ACCNO,
    A.DATASTATUS AS REPORT_STATUS_CODE,
    CASE TRIM(A.DATASTATUS)
        WHEN 'RCP' THEN '已簽收'
        WHEN 'VERIFY' THEN '等待報告'
        WHEN 'RPTCR' THEN '初步報告'
        WHEN 'CACHE' THEN '暫存報告'
        WHEN 'RPTVS' THEN '正式報告'
        ELSE A.DATASTATUS
    END AS REPORT_STATUS_NAME,
    A.REPORTID,
    B.MODIFYDR,
    B.MODIFYDATE
FROM RISWORKLISTDATAS A
LEFT JOIN RISREPORTDATAS B
    ON A.REPORTID = B.ID
WHERE A.HISGREQNO = :HISGREQNO
  AND A.HISREQNO = :HISREQNO
ORDER BY B.MODIFYDATE DESC NULLS LAST, A.ACCNO";

            return (sql, new Dictionary<string, object?>
            {
                ["HISGREQNO"] = hisGroupRequestNumber,
                ["HISREQNO"] = hisRequestNumber
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionImageReportTextQuery(
            string reportId)
        {
            const string sql = @"SELECT ID, REPORTID, ITEMNAME, LINENO, TEXTVALUE
FROM RISTEXTDATAS
WHERE REPORTID = :REPORTID
  AND ITEMNAME IN ('1', '2')
ORDER BY ITEMNAME, LINENO";

            return (sql, new Dictionary<string, object?>
            {
                ["REPORTID"] = reportId
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionLabResultsQuery(
            string chop1Date,
            string chop1Time,
            string chop1Room,
            int intop1No)
        {
            const string sql = @"SELECT DISTINCT
    ord.chOp4GReqNo,
    ord.chOp4ReqNo,
    ord.chAccNo,
    ord.chLReqNo,
    ord.chOp4OrdNo,
    rslt.CHSTAT,
    CASE rslt.CHSTAT
        WHEN '10' THEN '申請中'
        WHEN '30' THEN '已簽收'
        WHEN '50' THEN '部份報告'
        WHEN '60' THEN '報告確認'
        WHEN '70' THEN '報告複審'
        ELSE '其他'
    END AS CHSTAT_NAME,
    rslt.CHRPSEQ,
    rslt.CHORDNO,
    rslt.CHHEAD,
    rslt.CHNL,
    rslt.CHNH,
    rslt.CHVAL,
    rslt.VCHCORDNM,
    rslt.CHMDDTM,
    rslt.CHRCPDTM,
    rslt.CHUNIT,
    rslt.CHITEMSEQ,
    rslt.CHLREQNO,
    rslt.CHHEAD || ' ' || rslt.VCHCORDNM AS LAB_NAME
FROM OpdOrdTbl ord
LEFT JOIN AdmResultRpTbl rslt
    ON ord.chOp4GReqNo = rslt.chGReqNo
   AND ord.chOp4ReqNo = rslt.chReqNo
   AND RPAD(ord.chLReqNo, 11) = rslt.chLReqNo
WHERE (ord.chOp4ReqNo IS NOT NULL OR ord.chLReqNo IS NOT NULL)
  AND ord.chOp1Date = :CHOP1DATE
  AND ord.chOp1Time = :CHOP1TIME
  AND ord.chOp1Room = :CHOP1ROOM
  AND ord.intOp1No = :INTOP1NO
  AND rslt.CHTYPE = 'L'
  AND rslt.CHSTAT IN ('10', '30', '50', '60', '70')
ORDER BY rslt.CHLREQNO, rslt.CHITEMSEQ";

            return (sql, new Dictionary<string, object?>
            {
                ["CHOP1DATE"] = OracleParameterValue.Char(chop1Date, 7),
                ["CHOP1TIME"] = OracleParameterValue.Char(chop1Time, 1),
                ["CHOP1ROOM"] = OracleParameterValue.Char(chop1Room, 6),
                ["INTOP1NO"] = OracleParameterValue.Number(intop1No)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildInspectionTasksQuery(
            DateTime checkDateStart,
            DateTime checkDateEnd)
        {
            const string sql = @"SELECT
    H.BARCODESTR,
    H.ISRESERVE AS RESERVE_STATUS,
    CASE TRIM(H.ISRESERVE)
        WHEN '1' THEN '已預約'
        WHEN '0' THEN '預約取消'
        WHEN '2' THEN '佔名額'
        WHEN '4' THEN '已掛號'
        WHEN '5' THEN '已報到'
        ELSE '未知狀態'
    END AS STATUS_NAME,
    H.PATIENTNAME,
    H.PROJECTNO,
    P.PROJECTNAME,
    F.CHOP1DATE,
    F.CHOP1TIME,
    F.CHOP1ROOM,
    F.INTOP1NO,
    F.CHOP0DC
FROM
(
    SELECT
        BARCODESTR,
        ISRESERVE,
        PATIENTNAME,
        PROJECTNO
    FROM HealthCheckupList
    WHERE TRIM(ISRESERVE) NOT IN ('0', '2')
) H
LEFT JOIN HealthProjectList P
    ON H.PROJECTNO = P.PROJECTNO
LEFT JOIN FEMHCheckupList F
    ON H.BARCODESTR = F.BARCODESTR
    AND H.PROJECTNO = F.PROJECTNO
    AND NVL(F.CHOP0DC, 0) = 0
WHERE
    TRUNC(P.CHECKDATE) BETWEEN TRUNC(:CHECK_DATE_START) AND TRUNC(:CHECK_DATE_END)
    AND (
           F.CHOP1DATE IS NOT NULL
        OR F.CHOP1TIME IS NOT NULL
        OR F.CHOP1ROOM IS NOT NULL
        OR F.INTOP1NO IS NOT NULL
    )
ORDER BY F.CHOP1DATE DESC NULLS LAST,
         F.CHOP1TIME DESC NULLS LAST,
         H.BARCODESTR";

            return (sql, new Dictionary<string, object?>
            {
                ["CHECK_DATE_START"] = checkDateStart.Date,
                ["CHECK_DATE_END"] = checkDateEnd.Date
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildAccountSuiteQuery(
            string chop1Date, string chop1Time, string chop1Room, int intop1No)
        {
            const string sql = @"SELECT CHOP4ORDNAME AS SUITENAME,
       RLOP4AMT2 AS TOTAL,
       RLOP4SUB1 AS RECEIVABLE,
       RLOP4SUB3 AS DISCOUNT,
       RLOP4SUB5 AS BOOKED,
       RLOP4SUB6 AS ARREARS,
       INTOP1ORDSEQ AS ORDSEQ
FROM opdordtbl
WHERE CHOP1DATE = :CHOP1DATE
  AND CHOP1TIME = :CHOP1TIME
  AND CHOP1ROOM = :CHOP1ROOM
  AND INTOP1NO = :INTOP1NO
  AND RLOP4AMT2 <> '0'
  AND CHOP4STAT <> 'DC'
ORDER BY INTOP1ORDSEQ";
            return (sql, new Dictionary<string, object?>
            {
                ["CHOP1DATE"] = OracleParameterValue.Char(chop1Date, 7),
                ["CHOP1TIME"] = OracleParameterValue.Char(chop1Time, 1),
                ["CHOP1ROOM"] = OracleParameterValue.Char(chop1Room, 6),
                ["INTOP1NO"] = OracleParameterValue.Number(intop1No)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildAccountOrderNumberQuery(
            string chop1Date, string chop1Time, string chop1Room, string intop1No)
        {
            const string sql = @"SELECT F.CHOP4ORDNO
FROM FEMHCheckupList F
WHERE F.CHOP1DATE = :CHOP1DATE
  AND F.CHOP1TIME = :CHOP1TIME
  AND F.CHOP1ROOM = :CHOP1ROOM
  AND F.INTOP1NO = :INTOP1NO
  AND TRIM(F.CHOP0DC) <> '1'";
            return (sql, new Dictionary<string, object?>
            {
                ["CHOP1DATE"] = OracleParameterValue.Varchar2(chop1Date, 10),
                ["CHOP1TIME"] = OracleParameterValue.Varchar2(chop1Time, 2),
                ["CHOP1ROOM"] = OracleParameterValue.Varchar2(chop1Room, 10),
                ["INTOP1NO"] = OracleParameterValue.Varchar2(intop1No, 5)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildAccountOrderNamesQuery(
            string chop1Date, string chop1Time, string chop1Room, int intop1No, string excludedChop4OrdNo)
        {
            const string sql = @"SELECT CHOP4ORDNAME, CHOP4ORDNO, INTOP1ORDSEQ
FROM opdordtbl
WHERE CHOP1DATE = :CHOP1DATE
  AND CHOP1TIME = :CHOP1TIME
  AND CHOP1ROOM = :CHOP1ROOM
  AND INTOP1NO = :INTOP1NO
  AND TRIM(CHOP4ORDNO) IS NOT NULL
  AND TRIM(CHOP4ORDNO) <> :EXCLUDED_CHOP4ORDNO
ORDER BY INTOP1ORDSEQ";
            return (sql, new Dictionary<string, object?>
            {
                ["CHOP1DATE"] = OracleParameterValue.Char(chop1Date, 7),
                ["CHOP1TIME"] = OracleParameterValue.Char(chop1Time, 1),
                ["CHOP1ROOM"] = OracleParameterValue.Char(chop1Room, 6),
                ["INTOP1NO"] = OracleParameterValue.Number(intop1No),
                ["EXCLUDED_CHOP4ORDNO"] = OracleParameterValue.Varchar2(excludedChop4OrdNo, excludedChop4OrdNo.Length)
            });
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildAccountQueryList(
            DateTime startDate, DateTime endDate, string? keyword)
        {
            var sql = @"SELECT H.BARCODESTR, H.PROJECTNO, P.PROJECTNAME,
       TO_CHAR(H.RESERVEDATE, 'YYYY-MM-DD') AS RESERVEDATE,
       H.RESERVETIME,
       CASE TRIM(H.ISRESERVE)
           WHEN '4' THEN '已掛號'
           WHEN '5' THEN '已報到'
           ELSE '未知狀態'
       END AS STATUS_NAME,
       H.PATNO, H.PATIENTNAME, C.CUSTOMERNAME,
       F.CHOP1DATE, F.CHOP1TIME, F.CHOP1ROOM, F.INTOP1NO
FROM HealthCheckupList H
LEFT JOIN HealthCustomerList C ON C.CUSTOMERNO = H.CUSTOMERNO
LEFT JOIN HealthProjectList P ON P.PROJECTNO = H.PROJECTNO
JOIN FEMHCheckupList F ON F.BARCODESTR = H.BARCODESTR
    AND F.PROJECTNO = H.PROJECTNO
    AND TRIM(F.CHOP0DC) <> '1'
WHERE TRIM(H.ISRESERVE) NOT IN ('1', '0', '2')
    AND H.RESERVEDATE >= :START_DATE
    AND H.RESERVEDATE < :END_DATE_EXCLUSIVE";
            var parameters = new Dictionary<string, object?>
            {
                ["START_DATE"] = startDate.Date,
                ["END_DATE_EXCLUSIVE"] = endDate.Date.AddDays(1)
            };
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                sql += @"
    AND (UPPER(H.PROJECTNO) LIKE :KEYWORD
         OR UPPER(P.PROJECTNAME) LIKE :KEYWORD
         OR UPPER(H.PATIENTNAME) LIKE :KEYWORD
         OR UPPER(H.PATNO) LIKE :KEYWORD
         OR UPPER(C.CUSTOMERNAME) LIKE :KEYWORD
         OR UPPER(H.BARCODESTR) LIKE :KEYWORD)";
                parameters["KEYWORD"] = $"%{keyword.Trim().ToUpperInvariant()}%";
            }
            sql += "\nORDER BY H.RESERVEDATE DESC, H.RESERVETIME DESC, H.BARCODESTR";
            return (sql, parameters);
        }

        public static string GetConsultingDoctors()
        {
            return @"SELECT CHUSERID,
       CHUSERNAME,
       '(' || TRIM(CHUSERID) || ')' || ' ' || TRIM(CHUSERNAME) AS DOCTOREMP
FROM GENUSERPROFILE1
WHERE  CHENDDATE >= TO_CHAR(TO_NUMBER(TO_CHAR(SYSDATE, 'YYYYMMDD')) - 19110000)
  AND CHUSERSECTOR = '0294'
  AND CHUSERPOSI IN ('D6', '16', '35', '36', '37', '38')";
        }

        public static string getOPDMRBasicTbl(string lookupValue, string lookupCode)
        {
            var escapedLookupValue = OTHER.EscapeOracleLiteral(lookupValue);
            var patientWhereClause = $"PATNO='{escapedLookupValue}'";
            var opdWhereClause = $"CHMRNO='{escapedLookupValue}'";

            if (lookupCode == "idNo")
            {
                patientWhereClause = $"IDNUMBER='{escapedLookupValue}'";
                opdWhereClause = $"CHID='{escapedLookupValue}'";
            }
            else if (lookupCode == "name")
            {
                patientWhereClause = $"PATIENTNAME LIKE '{escapedLookupValue}%'";
                opdWhereClause = $"CHNAME LIKE '{escapedLookupValue}%'";
            }
            else if (lookupCode == "mobile")
            {
                patientWhereClause = $"MOBILE LIKE '{escapedLookupValue}%'";
                opdWhereClause = $"CHMRCELLULAR LIKE '{escapedLookupValue}%'";
            }

            return string.Format(@"SELECT
    PATNO AS chmrno,
    PATIENTNAME AS 病人姓名,
    IDNUMBER AS 病人身份證,
    GENDER AS 病人性別,
    AGE AS 年齡,
    BIRTHDAY AS 病人生日,
    MOBILE AS CHMRCELLULAR,
    CAST(NULL AS VARCHAR2(20)) AS CHBLOOD,
    TEL AS CHTELH,
    CAST(NULL AS VARCHAR2(100)) AS CHTELO,
    EMAIL,
    CONTACTNAME,
    CONTACTTEL,
    CONTACTRELATION,
    IS_VIP,
    FAMILYNOTE,
    SENDINGADDRESS,
    LIVEADDRESS,
    NOTICENOTE
FROM HealthCheckupPatient
WHERE {0}
UNION ALL
SELECT
    chmrno,
    chname AS 病人姓名,
    chid AS 病人身份證,
    CASE
        WHEN chsex = '1' THEN '男'
        WHEN chsex = '0' THEN '女'
        ELSE chsex
    END AS 病人性別,
    CASE
        WHEN chbirthday = ''
          OR chbirthday = '0000000'
          OR chbirthday = '0'
          OR chbirthday IS NULL
        THEN NULL
        ELSE TO_CHAR(
            TRUNC(
                MONTHS_BETWEEN(
                    SYSDATE,
                    TO_DATE(
                        TO_CHAR(TO_NUMBER(SUBSTR(chbirthday, 1, 3)) + 1911)
                            || SUBSTR(chbirthday, 4, 2)
                            || SUBSTR(chbirthday, 6, 2),
                        'YYYYMMDD'
                    )
                ) / 12
            )
        )
    END AS 年齡,
    CASE
        WHEN chbirthday = ''
          OR chbirthday = '0000000'
          OR chbirthday = '0'
          OR chbirthday IS NULL
        THEN NULL
        ELSE TO_CHAR((SUBSTR(chbirthday, 0, 3) + 1911))
            || SUBSTR(chbirthday, 4, 2)
            || SUBSTR(chbirthday, 6, 2)
    END AS 病人生日,
    CHMRCELLULAR,
    CHBLOOD,
    CHTELH,
    CHTELO,
    (
        SELECT MAX(EMAIL)
        FROM HealthCheckupList
        WHERE HealthCheckupList.PATNO = OPDMRBasicTbl.CHMRNO
          AND EMAIL IS NOT NULL
    ) AS EMAIL,
    CAST(NULL AS VARCHAR2(50)) AS CONTACTNAME,
    CAST(NULL AS VARCHAR2(100)) AS CONTACTTEL,
    CAST(NULL AS VARCHAR2(50)) AS CONTACTRELATION,
    CAST(NULL AS VARCHAR2(10)) AS IS_VIP,
    CAST(NULL AS VARCHAR2(4000)) AS FAMILYNOTE,
    CAST(NULL AS VARCHAR2(1000)) AS SENDINGADDRESS,
    CAST(NULL AS VARCHAR2(1000)) AS LIVEADDRESS,
    CAST(NULL AS VARCHAR2(4000)) AS NOTICENOTE
FROM OPDMRBasicTbl
WHERE {1}
  AND NOT EXISTS (
      SELECT 1
      FROM HealthCheckupPatient
      WHERE HealthCheckupPatient.PATNO = OPDMRBasicTbl.CHMRNO
  )", patientWhereClause, opdWhereClause);
        }

        public static string getAccount()
        {
            return string.Format(@"SELECT
    H_EMPNO,
    H_EMPNANE,
    H_DPTNAME,
    H_EMPPOSITION,
    STATUS,
    ROLE_CODE,
    TO_CHAR(CDATE, 'yyyy-MM-dd') AS CDATE
FROM account");
        }

        public static string getRoleData()
        {
            return string.Format(@"SELECT
    ROLE_CODE,
    ROLE_NAME,
    ROLE_COLOR
FROM HEALTH_CHECK_ROLE
ORDER BY ROLE_CODE");
        }

        public static string GetDashboardWorkAreas() => @"SELECT AREA_CODE, AREA_NAME
FROM HealthRESERVEAREATBL
WHERE STATUS = 'Y'
ORDER BY AREA_CODE, AREA_NAME";

        public static string GetDashboardTodayStatistics() => @"SELECT
    COUNT(*) AS TOTAL_COUNT,
    COUNT(CASE WHEN TRIM(ISRESERVE) = '1' THEN 1 END) AS RESERVED_COUNT,
    COUNT(CASE WHEN TRIM(ISRESERVE) = '4' THEN 1 END) AS REGISTERED_COUNT,
    COUNT(CASE WHEN TRIM(ISRESERVE) = '5' THEN 1 END) AS CHECKED_IN_COUNT,
    COUNT(CASE WHEN TRIM(ISRESERVE) = '6' THEN 1 END) AS CHECKING_COUNT
FROM HealthCheckupList
WHERE RESERVEDATE >= TRUNC(SYSDATE)
  AND RESERVEDATE < TRUNC(SYSDATE) + 1";

        public static string GetDashboardExaminees() => @"SELECT
    H.BARCODESTR, H.PROJECTNO, H.ISRESERVE,
    TO_CHAR(H.RESERVEDATE, 'YYYY-MM-DD') AS RESERVEDATE,
    H.RESERVETIME,
    H.PATNO, H.PATIENTNAME, H.IDNUMBER,
    H.GENDER, H.BIRTHDAY, H.WORKAREA,
    TO_CHAR(H.CHECKINTIME, 'YYYY/MM/DD') AS CHECKINDATE,
    TO_CHAR(H.CHECKINTIME, 'HH24:MI') AS CHECKINTIME
FROM HealthCheckupList H
WHERE H.RESERVEDATE >= :RESERVEDATE_START
  AND H.RESERVEDATE < :RESERVEDATE_END
  AND (:KEYWORD IS NULL
       OR INSTR(UPPER(H.PATIENTNAME), :KEYWORD) > 0
       OR INSTR(UPPER(H.IDNUMBER), :KEYWORD) > 0)
  AND (:WORKAREA IS NULL
       OR TRIM(H.WORKAREA) = :WORKAREA
       OR EXISTS (
           SELECT 1 FROM HealthRESERVEAREATBL A
           WHERE A.STATUS = 'Y'
             AND TRIM(A.AREA_CODE) = :WORKAREA
             AND TRIM(A.AREA_NAME) = TRIM(H.WORKAREA)
       ))
ORDER BY H.RESERVEDATE, H.RESERVETIME, H.BARCODESTR";

        public static string getHealthCheckupList() =>
            getHealthCheckupList("");

        public static string getHealthCheckupList(string? whereClause)
        {
            var normalizedWhereClause = string.IsNullOrWhiteSpace(whereClause)
                ? string.Empty
                : Environment.NewLine + "WHERE " + whereClause;

            return string.Format(@"SELECT
    CheckupList.*,
    CASE WHEN EXISTS (
        SELECT 1 FROM HealthCheckupSetInfo SetInfo
        WHERE SetInfo.BARCODESTR = CheckupList.BARCODESTR
          AND SetInfo.PROJECTNO = CheckupList.PROJECTNO
    ) OR EXISTS (
        SELECT 1 FROM HealthCheckupOtherSet OtherSet
        WHERE OtherSet.BARCODESTR = CheckupList.BARCODESTR
          AND OtherSet.PROJECTNO = CheckupList.PROJECTNO
    ) THEN '1' ELSE '0' END AS HAS_SELECTED_APPOINTMENT_ITEM,
    CASE WHEN EXISTS (
        SELECT 1 FROM HealthCheckupSetInfo SetInfo
        WHERE SetInfo.BARCODESTR = CheckupList.BARCODESTR
          AND SetInfo.PROJECTNO = CheckupList.PROJECTNO
          AND TRIM(SetInfo.CHDCTTYPE) IS NULL
    ) OR EXISTS (
        SELECT 1 FROM HealthCheckupOtherSet OtherSet
        WHERE OtherSet.BARCODESTR = CheckupList.BARCODESTR
          AND OtherSet.PROJECTNO = CheckupList.PROJECTNO
          AND TRIM(OtherSet.CHDCTTYPE) IS NULL
    ) THEN '1' ELSE '0' END AS HAS_MISSING_ACCOUNTING_UNIT,
    CustomerList.CUSTOMERNAME,
    ProjectList.PROJECTNAME
FROM (
    SELECT
        TRUNC(RESERVEDATE) AS RESERVEDATE_RAW,
        TRUNC(CHECKINTIME) AS CHECKINDATE_RAW,
        TO_CHAR(RESERVEDATE, 'yyyy/MM/dd') AS RESERVEDATE,
        RESERVETIME,
        TO_CHAR(CHECKINTIME, 'yyyy/MM/dd') AS CHECKINDATE,
        TO_CHAR(CHECKINTIME, 'HH24:mi') AS CHECKINTIME,
        REGTIME,
        DOCTOREMPNO,
        DOCTORNAME,
        ROOMNO,
        CATEGORY,
        BARCODESTR,
        PROJECTNO,
        PATIENTNAME,
        IDNUMBER,
        BIRTHDAY,
        GENDER,
        TEL,
        MOBILE,
        EMAIL,
        ISRESERVE,
        PATNO,
        CUSTOMERNO,
        SELFCOSTNO,
        WORKAREA,
        CREATEDAT
    FROM HealthCheckupList
) CheckupList
LEFT JOIN (
    SELECT
        CUSTOMERNO,
        CUSTOMERNAME
    FROM HealthCustomerList
) CustomerList
    ON CheckupList.CUSTOMERNO = CustomerList.CUSTOMERNO
LEFT JOIN (
    SELECT
        PROJECTNO,
        PROJECTNAME
    FROM HealthProjectList
) ProjectList
    ON CheckupList.PROJECTNO = ProjectList.PROJECTNO{0}
ORDER BY CheckupList.CREATEDAT DESC NULLS LAST, CheckupList.PROJECTNO DESC", normalizedWhereClause);
        }

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildAppointmentManagementQuery(
            DateTime? startDate,
            DateTime? endDate,
            string reservationArea,
            string reserveStatus,
            string keyword) =>
            BuildAppointmentManagementQueryCore(
                startDate,
                endDate,
                reservationArea,
                reserveStatus,
                keyword,
                onlyCheckInStatuses: false);

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildCheckInOperationQuery(
            DateTime? startDate,
            DateTime? endDate,
            string reservationArea,
            string reserveStatus,
            string keyword) =>
            BuildAppointmentManagementQueryCore(
                startDate,
                endDate,
                reservationArea,
                reserveStatus,
                keyword,
                onlyCheckInStatuses: true);

        public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildTodayCheckInQuery(
            string reservationArea,
            string reserveStatus,
            string keyword)
        {
            var whereClauses = new List<string>
            {
                "CheckupList.ISRESERVE IN ('4', '5')",
                "(CheckupList.RESERVEDATE_RAW = TRUNC(SYSDATE) OR CheckupList.CHECKINDATE_RAW = TRUNC(SYSDATE))"
            };
            var parameters = new Dictionary<string, object?>();

            if (!string.IsNullOrWhiteSpace(reservationArea))
            {
                whereClauses.Add("CheckupList.WORKAREA = :WORKAREA");
                parameters["WORKAREA"] = reservationArea;
            }

            if (!string.IsNullOrWhiteSpace(reserveStatus))
            {
                whereClauses.Add("CheckupList.ISRESERVE = :ISRESERVE");
                parameters["ISRESERVE"] = reserveStatus;
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                whereClauses.Add(@"(
    UPPER(CheckupList.PATIENTNAME) LIKE :KEYWORD
    OR UPPER(CheckupList.IDNUMBER) LIKE :KEYWORD
    OR UPPER(CheckupList.PROJECTNO) LIKE :KEYWORD
    OR UPPER(CheckupList.BARCODESTR) LIKE :KEYWORD
    OR UPPER(CheckupList.PATNO) LIKE :KEYWORD
    OR UPPER(CustomerList.CUSTOMERNAME) LIKE :KEYWORD
    OR UPPER(ProjectList.PROJECTNAME) LIKE :KEYWORD
)");
                parameters["KEYWORD"] = "%" + keyword.ToUpperInvariant() + "%";
            }

            var whereClause = string.Join(Environment.NewLine + "  AND ", whereClauses);
            return (getHealthCheckupList(whereClause), parameters);
        }

        private static (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildAppointmentManagementQueryCore(
            DateTime? startDate,
            DateTime? endDate,
            string reservationArea,
            string reserveStatus,
            string keyword,
            bool onlyCheckInStatuses)
        {
            var whereClauses = new List<string>();
            var parameters = new Dictionary<string, object?>();

            if (onlyCheckInStatuses)
            {
                whereClauses.Add("CheckupList.ISRESERVE IN ('4', '5')");
            }

            if (startDate.HasValue)
            {
                whereClauses.Add("CheckupList.RESERVEDATE_RAW >= :RESERVEDATE_START");
                parameters["RESERVEDATE_START"] = startDate.Value.Date;
            }

            if (endDate.HasValue)
            {
                whereClauses.Add("CheckupList.RESERVEDATE_RAW <= :RESERVEDATE_END");
                parameters["RESERVEDATE_END"] = endDate.Value.Date;
            }

            if (!string.IsNullOrWhiteSpace(reservationArea))
            {
                whereClauses.Add("CheckupList.WORKAREA = :WORKAREA");
                parameters["WORKAREA"] = reservationArea;
            }

            if (!string.IsNullOrWhiteSpace(reserveStatus))
            {
                whereClauses.Add("CheckupList.ISRESERVE = :ISRESERVE");
                parameters["ISRESERVE"] = reserveStatus;
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                whereClauses.Add(@"(
    UPPER(CheckupList.PATIENTNAME) LIKE :KEYWORD
    OR UPPER(CheckupList.IDNUMBER) LIKE :KEYWORD
    OR UPPER(CheckupList.PROJECTNO) LIKE :KEYWORD
    OR UPPER(CheckupList.BARCODESTR) LIKE :KEYWORD
    OR UPPER(CheckupList.PATNO) LIKE :KEYWORD
    OR UPPER(CustomerList.CUSTOMERNAME) LIKE :KEYWORD
    OR UPPER(ProjectList.PROJECTNAME) LIKE :KEYWORD
)");
                parameters["KEYWORD"] = "%" + keyword.ToUpperInvariant() + "%";
            }

            var whereClause = whereClauses.Count > 0
                ? string.Join(Environment.NewLine + "  AND ", whereClauses)
                : string.Empty;

            return (getHealthCheckupList(whereClause), parameters);
        }

        public static string getGroupAppointmentManagementList() =>
            getGroupAppointmentManagementList("", "", null, null, "");

        public static string getGroupAppointmentManagementList(
            string? customerNo,
            string? projectName,
            DateTime? checkDateStart,
            DateTime? checkDateEnd,
            string? reserveStatus)
        {
            var whereClauses = new List<string>();
            var normalizedCustomerNo = (customerNo ?? string.Empty).Trim();
            var normalizedProjectName = (projectName ?? string.Empty).Trim();
            var normalizedReserveStatus = (reserveStatus ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(normalizedCustomerNo))
            {
                whereClauses.Add($"CG.CUSTOMERNO = '{OTHER.EscapeOracleLiteral(normalizedCustomerNo)}'");
            }

            if (!string.IsNullOrWhiteSpace(normalizedProjectName))
            {
                whereClauses.Add($"UPPER(CG.PROJECTNAME) LIKE '%{OTHER.EscapeOracleLiteral(normalizedProjectName.ToUpperInvariant())}%'");
            }

            if (checkDateStart.HasValue)
            {
                whereClauses.Add($"TRUNC(CG.CHECKDATE) >= TO_DATE('{checkDateStart.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}', 'YYYYMMDD')");
            }

            if (checkDateEnd.HasValue)
            {
                whereClauses.Add($"TRUNC(CG.CHECKDATE) <= TO_DATE('{checkDateEnd.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}', 'YYYYMMDD')");
            }

            var whereClause = whereClauses.Count > 0
                ? Environment.NewLine + "    WHERE " + string.Join(Environment.NewLine + "      AND ", whereClauses)
                : string.Empty;
            var havingClause = normalizedReserveStatus is "0" or "1" or "2" or "4"
                ? Environment.NewLine + $"    HAVING COUNT(CASE WHEN CL.ISRESERVE = '{normalizedReserveStatus}' THEN 1 END) > 0"
                : string.Empty;

            return string.Format(@"SELECT *
FROM (
    SELECT
        CG.CUSTOMERNO,
        CG.CUSTOMERNAME,
        CG.PROJECTNO,
        CG.PROJECTNAME,
        CG.CHECKDATE,
        CG.ENDDATE,
        COUNT(CL.ISRESERVE) AS 預估人數,
        COUNT(CASE WHEN CL.ISRESERVE = '0' THEN 1 END) AS 預約取消,
        COUNT(CASE WHEN CL.ISRESERVE = '1' THEN 1 END) AS 預約人數,
        COUNT(CASE WHEN CL.ISRESERVE = '2' THEN 1 END) AS 佔名額,
        COUNT(CASE WHEN CL.ISRESERVE = '4' THEN 1 END) AS 已掛號
    FROM
    (
        SELECT
            HC.CUSTOMERNO,
            HC.CUSTOMERNAME,
            HP.PROJECTNO,
            HP.PROJECTNAME,
            HP.CHECKDATE,
            HP.ENDDATE
        FROM HealthCustomerList HC
        LEFT JOIN HealthProjectList HP
            ON HC.CUSTOMERNO = HP.CUSTOMERNO
    ) CG
    LEFT JOIN HealthCheckupList CL
        ON CG.PROJECTNO = CL.PROJECTNO{0}
    GROUP BY
        CG.CUSTOMERNO,
        CG.CUSTOMERNAME,
        CG.PROJECTNO,
        CG.PROJECTNAME,
        CG.CHECKDATE,
        CG.ENDDATE{1}
)
ORDER BY
    TO_NUMBER(預約人數) DESC", whereClause, havingClause);
        }

        public static string GetHealthCheckupListByBarcodeProject()
        {
            return @"SELECT
    BARCODESTR,
    PROJECTNO,
    RESERVEDATE,
    RESERVETIME,
    REGTIME,
    DOCTOREMPNO,
    DOCTORNAME,
    ROOMNO,
    PATIENTNAME,
    IDNUMBER,
    BIRTHDAY,
    GENDER,
    TEL,
    MOBILE,
    EMAIL,
    CATEGORY,
    ISRESERVE,
    PATNO,
    WORKAREA,
    CONTACTNAME,
    CONTACTTEL,
    CONTACTRELATION,
    MAILADDRESS,
    SENDINGADDRESS,
    LIVEADDRESS,
    ISSAMEADDRESS,
    CUSTOMERNO,
    SELFCOSTNO,
    CREATEDAT,
    UPDATEDAT,
    AGE,
    IS_FAMILY,
    IS_VIP,
    BranchFactory,
    EmployeeStudentNo
FROM HealthCheckupList
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO";
        }

        public static string GetFEMHCheckupRegistrationByBarcodeProject()
        {
            return @"SELECT
    CHOP1DATE,
    CHOP1TIME,
    CHOP1ROOM,
    INTOP1NO,
    INTOP1ORDSEQ,
    CHOP0DC
FROM FEMHCheckupList
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO
  AND NVL(CHOP0DC, 0) = 0";
        }

        public static string GetHealthCheckupNoteByBarcodeProject()
        {
            return @"SELECT
    FAMILYNOTE,
    NOTICENOTE,
    CURRENTNOTE,
    PERMANENTNOTE
FROM HealthCheckupNOTE
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO";
        }

        public static string GetHealthCheckupReserInfoByBarcodeProject()
        {
            return @"SELECT
    RESERVE_AREA,
    RESERVE_DATE,
    RESERVE_TIME,
    PARKING,
    LAXATIVE,
    MEAL
FROM HealthCheckupReserInfo
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO";
        }

        public static string GetHealthCheckupPaymentByBarcodeProject()
        {
            return @"SELECT
    PACKAGE_AMOUNT,
    EXTRA_AMOUNT,
    REMIT_AMOUNT,
    DISCOUNT_AMOUNT,
    ACCOUNT_AMOUNT,
    RECEIVED_AMOUNT,
    PAYMENT_DATE,
    PAYMENT_METHOD,
    NOTE
FROM HealthCheckupPayment
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO";
        }

        public static string GetHealthCheckupOtherSetByBarcodeProject()
        {
            return @"SELECT
    ITEMNO,
    SETCODE,
    SETNAME,
    AMOUNT,
    CHDCTTYPE
FROM HealthCheckupOtherSet
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO
ORDER BY ITEMNO";
        }

        public static string GetHealthCheckupQuestInfoByBarcodeProject()
        {
            return @"SELECT
    ITEMNO,
    QUESTIONNAIRE,
    AMOUNT
FROM HealthCheckupQuestInfo
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO
ORDER BY CASE WHEN REGEXP_LIKE(TO_CHAR(ITEMNO), '^[0-9]+$') THEN TO_NUMBER(TO_CHAR(ITEMNO)) END,
    TO_CHAR(ITEMNO)";
        }

        public static string GetHealthCheckupSetInfoByBarcodeProject()
        {
            return @"SELECT
    ITEMNO,
    LISITEMNAME,
    ORDTOT,
    SETNAME,
    AMOUNT,
    CHDCTTYPE
FROM HealthCheckupSetInfo
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO
ORDER BY CASE WHEN REGEXP_LIKE(TO_CHAR(ITEMNO), '^[0-9]+$') THEN TO_NUMBER(TO_CHAR(ITEMNO)) END,
    TO_CHAR(ITEMNO)";
        }

        public static string getSpecialCategoryTBL()
        {
            return @"SELECT
    CATEGORYNO,
    NAME,
    PUBLICEXPENSE,
    SUBSTR(TRIM(NAME), 1, 1) AS FIRSTCHAR
FROM SpecialCategoryTBL
ORDER BY
    FIRSTCHAR,
    NAME";
        }

        public static string getStaffData()
        {
            return string.Format(@"SELECT
    c.*,
    d.CHPLANAME AS H_DptName
FROM (
    SELECT
        H_EMPNO,
        H_EMPNANE,
        H_EMPPRSID,
        H_CARDPINCODE,
        H_EMPSTATUS,
        H_EMPEMAIL,
        H_EMPSEX,
        H_EMPPOSITION,
        H_EMPPSTID,
        H_EMPPSTLEVEL,
        CASE
            WHEN RTRIM(CHSECNO) IS NOT NULL THEN CHSECNO
            ELSE chusersector
        END AS H_DptID,
        CASE
            WHEN CHENDDATE = '9991231' THEN ''
            ELSE TO_CHAR(TO_NUMBER(CHENDDATE) + 19110000)
        END AS H_LeaveDate
    FROM (
        SELECT
            u.chuserid AS H_EmpNO,
            u.chusername AS H_EmpNane,
            u.chuserpid AS H_EmpPrsID,
            u.chusersector,
            '' AS H_CardPinCode,
            VCHSTATUS AS H_EmpStatus,
            CHUSEREMAIL AS H_EmpEmail,
            chusersex AS H_EmpSex,
            description AS H_EmpPosition,
            CHUSERPOSI AS H_EmpPstID,
            CHUSERTITLE AS H_EmpPstLevel,
            CHENDDATE
        FROM guid.genuserprofile1 u,
             emp.code2 s
        WHERE u.CHUSERPOSI = RTRIM(s.code)
          AND s.class = 'SER_CODE'
    ) a
    LEFT JOIN (
        SELECT
            CHDOCNO,
            CHSECNO
        FROM gendoctortbl
    ) b
        ON a.h_empno = b.CHDOCNO
) C
LEFT JOIN (
    SELECT
        CHPLANO,
        CHPLANAME
    FROM genplacetbl
) d
    ON RTRIM(c.H_DPTID) = RTRIM(d.CHPLANO)
WHERE h_empprsid IS NOT NULL
  AND h_empprsid <> '          '
  -- AND H_DPTNAME LIKE '健檢中心%'
  AND RTRIM(H_DPTID) = '0294'
  AND H_EMPSTATUS = '1'");
        }

        public static string GetHealthCheckupListReserveStatus()
        {
            return @"SELECT ISRESERVE
FROM HealthCheckupList
WHERE BARCODESTR = :BARCODESTR
  AND (:PROJECTNO IS NULL OR PROJECTNO = :PROJECTNO)";
        }

        public static string getHealthCustomerList()
        {
            return string.Format(@"SELECT
    CUSTOMERNO,
    CUSTOMERNAME,
    ADDRESS,
    TEL,
    FAX,
    EMAIL,
    CUSTOMERNOTE,
    ENGLISHNAME,
    UNIFORMNO,
    CONTACTPERSON,
    BUSINESSREGADDRESS,
    TAXOFFICECODE,
    BUSINESSUNION,
    INDUSTRYCATEGORY,
    BUSINESSUNITNAME,
    BUSINESSUNITADDRESS,
    MEDICALMANAGECODE,
    ACCOUNTINGUNIT,
    CASE
        WHEN TRIM(CUSTOMERNO) IS NOT NULL THEN '[' || TRIM(CUSTOMERNO) || '] ' || TRIM(CUSTOMERNAME)
        ELSE TRIM(CUSTOMERNAME)
    END AS CustUnit
FROM HealthCustomerList
WHERE CUSTOMERNAME IS NOT NULL
ORDER BY CUSTOMERNAME");
        }

        public static string getHealthPHRASENOTETBL()
        {
            return string.Format(@"SELECT
    CATEGORY,
    PHRASE_CONTENT
FROM HealthPHRASENOTETBL");
        }

        public static string getHealthRESERVEAREATBL()
        {
            return string.Format(@"SELECT
    AREA_CODE,
    AREA_NAME,
    STATUS
FROM HealthRESERVEAREATBL");
        }

        public static string getFlowListTBL()
        {
            return string.Format(@"SELECT
    NO,
    NAME
FROM FlowListTBL");
        }

        public static string getHealthProjectListForPersonalAppointment()
        {
            return string.Format(@"SELECT
    ProjectList.PROJECTNO,
    ProjectList.CUSTOMERNO,
    CustomerList.CUSTOMERNAME,
    ProjectList.CHECKYEAR,
    ProjectList.PROJECTNAME,
    ProjectList.NOTE
FROM (
    SELECT PROJECTNO,CUSTOMERNO,CHECKYEAR,PROJECTNAME,NOTE
    FROM HealthProjectList
      WHERE    TRUNC(ENDDATE)>=TRUNC(SYSDATE)
      AND ENDDATE IS NOT NULL
      AND  PROJECTNAME LIKE '%菁英%'
) ProjectList
LEFT JOIN HealthCustomerList CustomerList
    ON ProjectList.CUSTOMERNO = CustomerList.CUSTOMERNO
ORDER BY ProjectList.CHECKYEAR DESC");
        }

        public static string getHealthProjectList()
        {
            return string.Format(@"SELECT
    ProjectList.PROJECTNO,
    ProjectList.CREATEDATE,
    ProjectList.EDITDATE,
    ProjectList.CHECKDATE,
    ProjectList.CUSTOMERNO,
    ProjectList.SELFCOSTNO,
    CustomerList.CUSTOMERNAME,
    CASE
        WHEN TRIM(CustomerList.CUSTOMERNO) IS NOT NULL
        THEN '[' || TRIM(CustomerList.CUSTOMERNO) || '] '
             || TRIM(CustomerList.CUSTOMERNAME)
        ELSE TRIM(CustomerList.CUSTOMERNAME)
    END AS CustUnit,
    ProjectList.YEAR,
    ProjectList.CHECKYEAR,
    ProjectList.PROJECTNAME,
    ProjectList.NOTE,
    ProjectList.ENDDATE,
    ProjectList.COMPAREVAL,
    ProjectList.EXECAREA,
    ProjectList.FLOWNAME,
    ProjectList.QUESTIONNAIRENAME,
    ProjectList.ISPASTHISTORY,
    ProjectList.ISORALCHECK,
    ProjectList.ISONLINEQUESTION,
    ProjectList.STATUS
FROM (
    SELECT
        PROJECTNO,
        CREATEDATE,
        EDITDATE,
        CHECKDATE,
        CUSTOMERNO,
        SELFCOSTNO,
        YEAR,
        CHECKYEAR,
        PROJECTNAME,
        NOTE,
        ENDDATE,
        COMPAREVAL,
        EXECAREA,
        FLOWNAME,
        QUESTIONNAIRENAME,
        ISPASTHISTORY,
        ISORALCHECK,
        ISONLINEQUESTION,
        STATUS
    FROM HealthProjectList
WHERE    TRUNC(ENDDATE)>=TRUNC(SYSDATE)
) ProjectList
LEFT JOIN HealthCustomerList CustomerList
    ON ProjectList.CUSTOMERNO = CustomerList.CUSTOMERNO
ORDER BY
    CASE
        WHEN TRIM(ProjectList.CHECKYEAR) IS NULL THEN 1
        ELSE 0
    END,
    ProjectList.CHECKYEAR DESC,
    ProjectList.PROJECTNO DESC");
        }

        public static string getHealthProjectList(DateTime checkDateStart, DateTime checkDateEnd)
        {
            return getHealthProjectList().Replace(
                "TRUNC(ENDDATE)>=TRUNC(SYSDATE)",
                "TRUNC(CHECKDATE) BETWEEN TRUNC(:QUERY_START_DATE) AND TRUNC(:QUERY_END_DATE)",
                StringComparison.Ordinal);
        }

        public static string getSelfPayAccountingUnits()
        {
            return @"SELECT CHDCTTYPE,
       CHDCTTYPENAME,
       vchEDate AS VCHEDATE
FROM GENDCTTYPETBL
WHERE vchEDate IS NULL
   OR vchEDate >= TO_CHAR(SYSDATE, 'yyyyMMdd') - 19110000
ORDER BY
    CASE
        WHEN CHDCTTYPE = '00' THEN 0
        WHEN REGEXP_LIKE(CHDCTTYPE, '^[0-9]+$') THEN 1
        WHEN REGEXP_LIKE(CHDCTTYPE, '^[0-9]+[A-Za-z]+') THEN 2
        ELSE 3
    END,
    TO_NUMBER(REGEXP_SUBSTR(CHDCTTYPE, '^[0-9]+')),
    CHDCTTYPE";
        }

        public static string getSelfPayAccountingUnitDisplay()
        {
            return @"SELECT '[' || CHDCTTYPE || '] ' || CHDCTTYPENAME AS DCTTYPE_DISPLAY
FROM GENDCTTYPETBL
WHERE CHDCTTYPE = :SELFCOSTNO";
        }

        public static string getCheckProjectScheme(string PROJECTNO)
        {
            var escapedProjectNo = OTHER.EscapeOracleLiteral(PROJECTNO);

            return string.Format(@"SELECT
    SCHEMENO,
    LISITEMNAME,
    NAME,
    NOTE,
    PUBLICEXPENSE,
    CHDCTTYPE,
    FIXITEMS
FROM CheckProjectScheme
WHERE PROJECTNO = '{0}'
ORDER BY
    CASE
        WHEN REGEXP_LIKE(TRIM(SCHEMENO), '^[[:digit:]]+$') THEN TO_NUMBER(TRIM(SCHEMENO))
        ELSE NULL
    END,
    SCHEMENO", escapedProjectNo);
        }

        public static string GetPackageMaintenanceList()
        {
            return @"SELECT
    b.CHECKDATE,
    b.PROJECTNAME,
    b.PROJECTNO,
    a.LISITEMNAME,
    a.NAME,
    a.SCHEMECODE,
    b.CUSTOMERNAME
FROM (
    SELECT DISTINCT PROJECTNO, NVL(LISITEMNAME, NAME) AS LISITEMNAME, NAME, SCHEMECODE
    FROM CheckProjectScheme
) a
LEFT JOIN (
    SELECT
        ProjectList.PROJECTNO,
        ProjectList.PROJECTNAME,
        CustomerList.CUSTOMERNAME,
        ProjectList.CHECKDATE
    FROM HealthProjectList ProjectList
    LEFT JOIN HealthCustomerList CustomerList
        ON ProjectList.CUSTOMERNO = CustomerList.CUSTOMERNO
    WHERE ProjectList.CHECKDATE >= :START_DATE
      AND ProjectList.CHECKDATE < :END_DATE_EXCLUSIVE
) b
    ON a.PROJECTNO = b.PROJECTNO
WHERE b.PROJECTNO IS NOT NULL
  AND UPPER(NVL(b.PROJECTNAME, ' ')) LIKE UPPER(:PROJECT_NAME) ESCAPE '\'
  AND UPPER(NVL(a.PROJECTNO, ' ')) LIKE UPPER(:PROJECT_NO) ESCAPE '\'
  AND UPPER(NVL(a.NAME, ' ')) LIKE UPPER(:PACKAGE_NAME) ESCAPE '\'
  AND UPPER(NVL(a.SCHEMECODE, ' ')) LIKE UPPER(:SCHEME_CODE) ESCAPE '\'
  AND UPPER(NVL(b.CUSTOMERNAME, ' ')) LIKE UPPER(:CUSTOMER_NAME) ESCAPE '\'
ORDER BY b.CHECKDATE DESC NULLS LAST,
         a.PROJECTNO,
         a.LISITEMNAME,
         a.SCHEMECODE";
        }

        public static string getCheckItemCategories()
        {
            return @"SELECT CHECKCATEGORY
FROM (
    SELECT DISTINCT CHECKCATEGORY
    FROM CheckItem
    WHERE CHECKCATEGORY IS NOT NULL
)
ORDER BY CASE WHEN TRIM(CHECKCATEGORY) = '基本體格檢查' THEN 0 ELSE 1 END,
         CHECKCATEGORY";
        }

        public static string getCheckItemItemCategories()
        {
            return @"SELECT DISTINCT ITEMCATEGORY
FROM CheckItem
ORDER BY ITEMCATEGORY";
        }

        public static string getCheckItemsByCategory(string CHECKCATEGORY, string KEYWORD)
        {
            var whereClauses = new List<string>
            {
                "CHECKCATEGORY IS NOT NULL"
            };

            if (!string.IsNullOrWhiteSpace(CHECKCATEGORY))
            {
                var escapedCategory = OTHER.EscapeOracleLiteral(CHECKCATEGORY.Trim().ToUpperInvariant());
                whereClauses.Add(string.Format("UPPER(CHECKCATEGORY) LIKE '%{0}%'", escapedCategory));
            }

            if (!string.IsNullOrWhiteSpace(KEYWORD))
            {
                var escapedKeyword = OTHER.EscapeOracleLiteral(KEYWORD.Trim().ToUpperInvariant());
                whereClauses.Add(string.Format(@"(
    UPPER(CHECKNO) LIKE '%{0}%'
    OR UPPER(NAME) LIKE '%{0}%'
    OR UPPER(ENAME) LIKE '%{0}%'
    OR UPPER(ITEMCATEGORY) LIKE '%{0}%'
    OR UPPER(REPORTNAME) LIKE '%{0}%'
)", escapedKeyword));
            }

            return string.Format(@"SELECT CHECKNO, NAME, CHECKCATEGORY, ITEMCATEGORY,
       REPORTNAME, ENAME, FORMAT, UNIT
FROM CheckItem
WHERE {0}
ORDER BY CASE WHEN TRIM(CHECKCATEGORY) = '基本體格檢查' THEN 0 ELSE 1 END,
         CHECKCATEGORY,
         CHECKNO", string.Join(@"
  AND ", whereClauses));
        }

        public static string getHealthCustomer(string CUSTOMERNO)
        {
            return string.Format(@"SELECT
    CUSTOMERNO,
    CUSTOMERNAME,
    ADDRESS,
    TEL,
    FAX,
    EMAIL,
    CUSTOMERNOTE,
    ENGLISHNAME,
    UNIFORMNO,
    CONTACTPERSON,
    BUSINESSREGADDRESS,
    TAXOFFICECODE,
    BUSINESSUNION,
    INDUSTRYCATEGORY,
    BUSINESSUNITNAME,
    BUSINESSUNITADDRESS,
    MEDICALMANAGECODE,
    ACCOUNTINGUNIT
FROM HealthCustomerList
WHERE CUSTOMERNO = '{0}'", OTHER.EscapeOracleLiteral(CUSTOMERNO));
        }

        public static string getHealthCustomerProjects()
        {
            return @"SELECT
    PROJECTNAME,
    PROJECTNO,
    CHECKYEAR
FROM HealthProjectList
WHERE CUSTOMERNO = :CUSTOMERNO
ORDER BY CHECKYEAR DESC";
        }

        public static string GetHealthProjectListCount(string PROJECTNO)
        {
            return string.Format(@"SELECT COUNT(1) AS ITEMCOUNT
FROM HealthProjectList
WHERE PROJECTNO = '{0}'", OTHER.EscapeOracleLiteral(PROJECTNO));
        }

        public static string getCheckProjectSpecialItemTbl(string PROJECTNO)
        {
            var escapedProjectNo = OTHER.EscapeOracleLiteral(PROJECTNO);

            return string.Format(@" SELECT A.SPECIALITEMNO, B.NAME, A.PUBLICEXPENSE FROM(
select SPECIALITEMNO, PUBLICEXPENSE from CheckProjectSpecialItemTbl
WHERE PROJECTNO= '{0}'
)A
LEFT JOIN
(select CATEGORYNO, NAME from  SpecialCategoryTBL)B
ON   A.SPECIALITEMNO = B.CATEGORYNO", escapedProjectNo);
        }

        public static string getCheckItemsByCodes(string CHECKNOS)
        {
            var checkNoInList = OTHER.BuildOracleInList(CHECKNOS, true);

            if (string.IsNullOrWhiteSpace(checkNoInList))
            {
                return @"SELECT CHECKNO, NAME, ITEMCATEGORY, REPORTNAME, CHECKCATEGORY
FROM CheckItem
WHERE 1 = 0";
            }

            return string.Format(@"SELECT
    TRIM(CHECKNO) AS CHECKNO,
    NAME,
    ITEMCATEGORY,
    REPORTNAME,
    CHECKCATEGORY
FROM CheckItem
WHERE UPPER(TRIM(CHECKNO)) IN ({0})
ORDER BY CHECKNO", checkNoInList);
        }

        public static string getInternalMedicalOrderItemsByCodes(string CHECKNOS)
        {
            var checkNoInList = OTHER.BuildOracleInList(CHECKNOS, true);

            if (string.IsNullOrWhiteSpace(checkNoInList))
            {
                return @"SELECT
    CAST(NULL AS VARCHAR2(50)) AS CHECKNO,
    CAST(NULL AS VARCHAR2(200)) AS NAME,
    CAST(NULL AS VARCHAR2(50)) AS ITEMCATEGORY,
    CAST(NULL AS VARCHAR2(200)) AS REPORTNAME,
    CAST(NULL AS VARCHAR2(50)) AS CHECKCATEGORY
FROM dual
WHERE 1 = 0";
            }

            return string.Format(@"SELECT CHECKNO, NAME, ITEMCATEGORY, REPORTNAME, CHECKCATEGORY
FROM (
    SELECT
        TRIM(A.CHPRICENOKID) AS CHECKNO,
        NVL(TRIM(B.CHORDEXTNAME), TRIM(A.CHPRICENOKID)) AS NAME,
        '院內組套' AS ITEMCATEGORY,
        NVL(TRIM(B.CHORDEXTNAME), TRIM(A.CHPRICENOKID)) AS REPORTNAME,
        '院內組套' AS CHECKCATEGORY,
        ROW_NUMBER() OVER (
            PARTITION BY UPPER(TRIM(A.CHPRICENOKID))
            ORDER BY TRIM(A.CHPRICENO)
        ) AS RN
    FROM GenMultiOrderTbl A
    LEFT JOIN GenOrdBasicExtTbl B
        ON UPPER(TRIM(A.CHPRICENOKID)) = UPPER(TRIM(B.chOrdextNo))
    WHERE UPPER(TRIM(A.CHPRICENOKID)) IN ({0})
)
WHERE RN = 1
ORDER BY CHECKNO", checkNoInList);
        }

        public static string getProjectCheckItem(string FIXITEMS)
        {
            var checkNoInList = OTHER.BuildOracleInList(FIXITEMS, true);

            if (string.IsNullOrWhiteSpace(checkNoInList))
            {
                return @"SELECT CHECKNO, NAME
FROM CheckItem
WHERE 1 = 0";
            }

            return string.Format(@"SELECT
    TRIM(CHECKNO) AS CHECKNO,
    NAME
FROM CheckItem
WHERE UPPER(TRIM(CHECKNO)) IN ({0})
ORDER BY CHECKNO", checkNoInList);
        }

        public static string getInternalMedicalOrderSuites(string? orderCode, string? orderName)
        {
            var whereClauses = new List<string>
            {
                "TRUNC(a.CHORDPRIDATE) >= :START_DATE",
                "TRUNC(a.CHORDPRIDATE) <= :END_DATE"
            };

            if (!string.IsNullOrWhiteSpace(orderCode))
            {
                whereClauses.Add("UPPER(TRIM(a.CHORDNO)) LIKE :ORDER_CODE");
            }

            if (!string.IsNullOrWhiteSpace(orderName))
            {
                whereClauses.Add("UPPER(TRIM(a.CHORDENAME)) LIKE :ORDER_NAME");
            }

            return string.Format(@"SELECT
    a.CHORDNO,
    a.CHORDENAME,
    a.RLORDPRIA2 AS SELF_PAY_AMOUNT,
    TO_CHAR(a.CHORDPRIDATE, 'YYYY/MM/DD') AS PRICE_DATE,
    b.ORDER_COUNT
FROM
(
    SELECT
        TRIM(CHORDNO) AS CHORDNO,
        RTRIM(CHORDENAME) AS CHORDENAME,
        RLORDPRIA2,
        CHORDPRIDAY,
        TO_DATE(
            TO_CHAR(TO_NUMBER(SUBSTR(TO_CHAR(CHORDPRIDAY), 1, 3)) + 1911)
                || SUBSTR(TO_CHAR(CHORDPRIDAY), 4, 4),
            'YYYYMMDD'
        ) AS CHORDPRIDATE
    FROM genordbasictbl
    WHERE TRIM(chordno) LIKE '294-%'
) a
JOIN
(
    SELECT TRIM(CHPRICENO) AS CHPRICENO, COUNT(*) AS ORDER_COUNT
    FROM GenMultiOrderTbl
    GROUP BY TRIM(CHPRICENO)
) b
    ON UPPER(TRIM(a.CHORDNO)) = UPPER(TRIM(b.CHPRICENO))
WHERE {0}
ORDER BY a.CHORDPRIDATE DESC", string.Join(Environment.NewLine + "  AND ", whereClauses));
        }

        public static string getInternalMedicalOrderSuiteOrders()
        {
            return @"SELECT
    A.CHPRICENO,
    A.CHPRICENOKID,
    B.CHORDEXTNAME
FROM
(
    SELECT TRIM(CHPRICENO) AS CHPRICENO, TRIM(CHPRICENOKID) AS CHPRICENOKID
    FROM GenMultiOrderTbl
    WHERE UPPER(TRIM(CHPRICENO)) = UPPER(TRIM(:CHPRICENO))
) A
LEFT JOIN
(
    SELECT TRIM(chOrdextNo) AS chOrdextNo, RTRIM(CHORDEXTNAME) AS CHORDEXTNAME
    FROM GenOrdBasicExtTbl
) B
    ON UPPER(TRIM(A.CHPRICENOKID)) = UPPER(TRIM(B.chOrdextNo))
ORDER BY A.CHPRICENOKID";
        }

        public static string getSYSTIMESTAMP()
        {
           return string.Format(@" SELECT TO_CHAR(SYSTIMESTAMP, 'YYYYMMDDHH24MISSFF3') AS dt_ms FROM dual");
        }

        public static string getCheckGlobalScheme()
        {
            return string.Format(@"SELECT PROJECTNO,PROJECTNAME FROM CheckGlobalScheme");
        }
    }

    #endregion

    #region UPDATE - 更新語法函式

    public static class UPDATE
    {
        public static string CheckInRegisteredAppointment()
        {
            return @"UPDATE HealthCheckupList
SET ISRESERVE = '5',
    CHECKINTIME = SYSDATE
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO
  AND ISRESERVE = '4'";
        }

        public static string CancelCheckedInAppointment()
        {
            return @"UPDATE HealthCheckupList
SET ISRESERVE = '4'
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO
  AND ISRESERVE = '5'";
        }

        public static string UpdateFEMHCheckupCancellationStatus()
        {
            return @"UPDATE FEMHCheckupList
SET CHOP0DC = 1,
    CDATE = SYSDATE
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO
  AND NVL(CHOP0DC, 0) = 0";
        }

        public static string UpdateAccountRoleStatus(string H_EMPNO, string ROLE_CODE, string STATUS)
        {
            return string.Format(@"UPDATE ACCOUNT
SET
    ROLE_CODE = '{0}',
    STATUS = '{1}'
WHERE H_EMPNO = '{2}'",
                OTHER.EscapeOracleLiteral(ROLE_CODE),
                OTHER.EscapeOracleLiteral(STATUS),
                OTHER.EscapeOracleLiteral(H_EMPNO));
        }

        public static string UPdateHealthCheckupList(string BARCODESTR, string ISRESERVE)
        {
            return string.Format(@"UPDATE HealthCheckupList
SET ISRESERVE = '{1}'
WHERE BARCODESTR = '{0}'", BARCODESTR, ISRESERVE);
        }

        public static string UpdateHealthCheckupListRegistered()
        {
            return @"UPDATE HealthCheckupList
SET ISRESERVE = '4'
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO";
        }

        public static string UpdateHealthCheckupListReserveStatus()
        {
            return @"UPDATE HealthCheckupList
SET ISRESERVE = :ISRESERVE,
    UPDATEDAT = SYSDATE
WHERE BARCODESTR = :BARCODESTR
  AND (PROJECTNO = :PROJECTNO OR (:PROJECTNO IS NULL AND PROJECTNO IS NULL))";
        }

        public static string UpdateCheckProjectSchemeCode()
        {
            return @"UPDATE CheckProjectScheme
SET SCHEMECODE = :SCHEME_CODE
WHERE PROJECTNO = :PROJECT_NO
  AND NVL(LISITEMNAME, NAME) = :LIS_ITEM_NAME";
        }

        public static string UpdateCheckItem()
        {
            return @"UPDATE CheckItem
SET CHECKNO = :CHECKNO,
    NAME = :NAME,
    CHECKCATEGORY = :CHECKCATEGORY,
    ITEMCATEGORY = :ITEMCATEGORY,
    ENAME = :ENAME,
    FORMAT = :FORMAT,
    UNIT = :UNIT
WHERE CHECKNO = :ORIGINAL_CHECKNO";
        }

        public static string UpdateHealthCustomer()
        {
            return string.Format(@"UPDATE HealthCustomerList
SET
    CUSTOMERNAME = :CUSTOMERNAME,
    ENGLISHNAME = :ENGLISHNAME,
    UNIFORMNO = :UNIFORMNO,
    CONTACTPERSON = :CONTACTPERSON,
    ADDRESS = :ADDRESS,
    TEL = :TEL,
    FAX = :FAX,
    EMAIL = :EMAIL,
    BUSINESSREGADDRESS = :BUSINESSREGADDRESS,
    TAXOFFICECODE = :TAXOFFICECODE,
    BUSINESSUNION = :BUSINESSUNION,
    INDUSTRYCATEGORY = :INDUSTRYCATEGORY,
    BUSINESSUNITNAME = :BUSINESSUNITNAME,
    BUSINESSUNITADDRESS = :BUSINESSUNITADDRESS,
    MEDICALMANAGECODE = :MEDICALMANAGECODE,
    ACCOUNTINGUNIT = :ACCOUNTINGUNIT,
    CUSTOMERNOTE = :CUSTOMERNOTE
WHERE CUSTOMERNO = :CUSTOMERNO");
        }

        public static string UpdateHealthProjectList()
        {
            return @"
UPDATE HealthProjectList
SET
    EDITDATE = :EDITDATE,
    CHECKDATE = :CHECKDATE,
    CUSTOMERNO = :CUSTOMERNO,
    SELFCOSTNO = :SELFCOSTNO,
    YEAR = :YEAR,
    CHECKYEAR = :CHECKYEAR,
    PROJECTNAME = :PROJECTNAME,
    NOTE = :NOTE,
    COMPAREVAL = :COMPAREVAL,
    ENDDATE = :ENDDATE,
    EXECAREA = :EXECAREA,
    FLOWNO = :FLOWNO,
    FLOWNAME = :FLOWNAME,
    QUESTIONNAIRENO = :QUESTIONNAIRENO,
    QUESTIONNAIRENAME = :QUESTIONNAIRENAME,
    ISPASTHISTORY = :ISPASTHISTORY,
    ISORALCHECK = :ISORALCHECK,
    ISONLINEQUESTION = :ISONLINEQUESTION,
    STATUS = :STATUS,
    CREATEUSERNO = :CREATEUSERNO
WHERE PROJECTNO = :PROJECTNO";
        }

        public static string UPDATEHealthCheckupList()
        {
            return string.Format(@"UPDATE HealthCheckupList
SET
    BARCODESTR       = :BARCODESTR,
    PROJECTNO        = :PROJECTNO,
    RESERVEDATE      = :RESERVEDATE,
    RESERVETIME      = :RESERVETIME,
    REGTIME          = :REGTIME,
    DOCTOREMPNO      = :DOCTOREMPNO,
    DOCTORNAME       = :DOCTORNAME,
    ROOMNO           = :ROOMNO,
    PATIENTNAME      = :PATIENTNAME,
    IDNUMBER         = :IDNUMBER,
    BIRTHDAY         = :BIRTHDAY,
    GENDER           = :GENDER,
    TEL              = :TEL,
    MOBILE           = :MOBILE,
    EMAIL            = :EMAIL,
    CATEGORY         = :CATEGORY,
    ISRESERVE        = :ISRESERVE,
    PATNO            = :PATNO,
    WORKAREA         = :WORKAREA,
    CONTACTNAME      = :CONTACTNAME,
    CONTACTTEL       = :CONTACTTEL,
    CONTACTRELATION  = :CONTACTRELATION,
    MAILADDRESS      = :MAILADDRESS,
    SENDINGADDRESS   = :SENDINGADDRESS,
    LIVEADDRESS      = :LIVEADDRESS,
    ISSAMEADDRESS    = :ISSAMEADDRESS,
    CUSTOMERNO       = :CUSTOMERNO,
    SELFCOSTNO       = :SELFCOSTNO,
    UPDATEDAT        = SYSDATE,
    AGE              = :AGE,
    IS_FAMILY        = :IS_FAMILY,
    IS_VIP           = :IS_VIP,
    BranchFactory    = :BranchFactory,
    EmployeeStudentNo = :EmployeeStudentNo
WHERE BARCODESTR = :ORIGINAL_BARCODESTR
  AND PROJECTNO = :ORIGINAL_PROJECTNO");
    
        }
    }

    #endregion

    #region DELETE - 刪除語法函式

    public static class DELETE
    {
        public static string DELETEHealthCheckupSetInfo()
        {
            return @"DELETE FROM HealthCheckupSetInfo
WHERE BARCODESTR = :BARCODESTR
  AND PROJECTNO = :PROJECTNO";
        }

        public static string DELETEHealthCheckupOtherSet()
        {
            return @"DELETE FROM HealthCheckupOtherSet
WHERE PROJECTNO = :PROJECTNO
  AND BARCODESTR = :BARCODESTR";
        }

        public static string DELETEHealthCheckupQuestInfo()
        {
            return @"DELETE FROM HealthCheckupQuestInfo
WHERE PROJECTNO = :PROJECTNO
  AND BARCODESTR = :BARCODESTR";
        }

        public static string DeletedAccount(string H_EMPNO)
        {
            return string.Format(@"DELETE account
WHERE H_EMPNO = '{0}'", H_EMPNO);
        }

        public static string DELETEHealthCheckupList(string BARCODESTR)
        {
            return string.Format(@"DELETE HealthCheckupList
WHERE BARCODESTR = '{0}'", BARCODESTR);
        }

        public static string DeleteCheckItem()
        {
            return @"DELETE FROM CheckItem
WHERE CHECKNO = :CHECKNO";
        }

        public static string DeleteHealthCustomer(string CUSTOMERNO)
        {
            return string.Format(@"DELETE FROM HealthCustomerList
WHERE CUSTOMERNO = '{0}'", OTHER.EscapeOracleLiteral(CUSTOMERNO));
        }

        public static string DeleteHealthProjectList()
        {
            return @"DELETE HealthProjectList
WHERE PROJECTNO = :PROJECTNO";
        }

        public static string DeleteCheckProjectScheme()
        {
            return @"DELETE FROM CheckProjectScheme
WHERE PROJECTNO = :PROJECTNO";
        }

        public static string DeleteCheckProjectSpecialItemTbl()
        {
            return @"DELETE FROM CheckProjectSpecialItemTbl
WHERE PROJECTNO = :PROJECTNO";
        }
    }

    #endregion

    #region INSERT - 新增語法函式

    public static class INSERT
    {
        public static string InsertHealthCheckupList()
        {
            return @"INSERT INTO HealthCheckupList (
    BARCODESTR,
    PROJECTNO,
    RESERVEDATE,
    RESERVETIME,
    REGTIME,
    DOCTOREMPNO,
    DOCTORNAME,
    ROOMNO,
    PATIENTNAME,
    IDNUMBER,
    BIRTHDAY,
    AGE,
    GENDER,
    TEL,
    MOBILE,
    EMAIL,
    CATEGORY,
    ISRESERVE,
    PATNO,
    WORKAREA,
    CONTACTNAME,
    CONTACTTEL,
    CONTACTRELATION,
    MAILADDRESS,
    SENDINGADDRESS,
    LIVEADDRESS,
    ISSAMEADDRESS,
    IS_VIP,
    IS_FAMILY,
    BranchFactory,
    EmployeeStudentNo,
    CUSTOMERNO,
    SELFCOSTNO,
    CREATEDAT
) VALUES (
    :BARCODESTR,
    :PROJECTNO,
    :RESERVEDATE,
    :RESERVETIME,
    :REGTIME,
    :DOCTOREMPNO,
    :DOCTORNAME,
    :ROOMNO,
    :PATIENTNAME,
    :IDNUMBER,
    :BIRTHDAY,
    :AGE,
    :GENDER,
    :TEL,
    :MOBILE,
    :EMAIL,
    :CATEGORY,
    :ISRESERVE,
    :PATNO,
    :WORKAREA,
    :CONTACTNAME,
    :CONTACTTEL,
    :CONTACTRELATION,
    :MAILADDRESS,
    :SENDINGADDRESS,
    :LIVEADDRESS,
    :ISSAMEADDRESS,
    :IS_VIP,
    :IS_FAMILY,
    :BranchFactory,
    :EmployeeStudentNo,
    :CUSTOMERNO,
    :SELFCOSTNO,
    SYSDATE
)";
        }

        public static string InsertHealthCheckupNote()
        {
            return @"INSERT INTO HealthCheckupNOTE (
    BARCODESTR,
    PROJECTNO,
    FAMILYNOTE,
    NOTICENOTE,
    CURRENTNOTE,
    PERMANENTNOTE,
    CREATEDAT
) VALUES (
    :BARCODESTR,
    :PROJECTNO,
    :FAMILYNOTE,
    :NOTICENOTE,
    :CURRENTNOTE,
    :PERMANENTNOTE,
    SYSDATE
)";
        }

        public static string InsertHealthCheckupReserInfo()
        {
            return @"INSERT INTO HealthCheckupReserInfo (
    PROJECTNO,
    BARCODESTR,
    RESERVE_AREA,
    RESERVE_DATE,
    RESERVE_TIME,
    PARKING,
    LAXATIVE,
    MEAL
) VALUES (
    :PROJECTNO,
    :BARCODESTR,
    :RESERVE_AREA,
    :RESERVE_DATE,
    :RESERVE_TIME,
    :PARKING,
    :LAXATIVE,
    :MEAL
)";
        }

        public static string InsertHealthCheckupSetInfo()
        {
            return @"INSERT INTO HealthCheckupSetInfo (
    PROJECTNO,
    BARCODESTR,
    ITEMNO,
    LISITEMNAME,
    ORDTOT,
    SETNAME,
    AMOUNT,
    CHDCTTYPE,
    CREATEDAT
) VALUES (
    :PROJECTNO,
    :BARCODESTR,
    :ITEMNO,
    :LISITEMNAME,
    :ORDTOT,
    :SETNAME,
    :AMOUNT,
    :CHDCTTYPE,
    SYSDATE
)";
        }

        public static string InsertHealthCheckupOtherSet()
        {
            return @"INSERT INTO HealthCheckupOtherSet (
    PROJECTNO,
    BARCODESTR,
    ITEMNO,
    SETCODE,
    SETNAME,
    AMOUNT,
    CHDCTTYPE,
    CREATEDAT
) VALUES (
    :PROJECTNO,
    :BARCODESTR,
    :ITEMNO,
    :SETCODE,
    :SETNAME,
    :AMOUNT,
    :CHDCTTYPE,
    SYSDATE
)";
        }

        public static string InsertHealthCheckupQuestInfo()
        {
            return @"INSERT INTO HealthCheckupQuestInfo (
    PROJECTNO,
    BARCODESTR,
    ITEMNO,
    QUESTIONNAIRE,
    AMOUNT,
    CREATEDAT
) VALUES (
    :PROJECTNO,
    :BARCODESTR,
    :ITEMNO,
    :QUESTIONNAIRE,
    :AMOUNT,
    SYSDATE
)";
        }

        public static string InsertHealthCheckupPayment()
        {
            return @"INSERT INTO HealthCheckupPayment (
    PROJECTNO,
    BARCODESTR,
    PACKAGE_AMOUNT,
    EXTRA_AMOUNT,
    REMIT_AMOUNT,
    DISCOUNT_AMOUNT,
    ACCOUNT_AMOUNT,
    RECEIVED_AMOUNT,
    PAYMENT_DATE,
    PAYMENT_METHOD,
    NOTE,
    CREATEDAT
) VALUES (
    :PROJECTNO,
    :BARCODESTR,
    :PACKAGE_AMOUNT,
    :EXTRA_AMOUNT,
    :REMIT_AMOUNT,
    :DISCOUNT_AMOUNT,
    :ACCOUNT_AMOUNT,
    :RECEIVED_AMOUNT,
    :PAYMENT_DATE,
    :PAYMENT_METHOD,
    :NOTE,
    SYSDATE
)";
        }

        public static string InsertCheckItem()
        {
            return @"INSERT INTO CheckItem (
    CHECKNO, NAME, CHECKCATEGORY, ITEMCATEGORY, ENAME, FORMAT, UNIT
) VALUES (
    :CHECKNO, :NAME, :CHECKCATEGORY, :ITEMCATEGORY, :ENAME, :FORMAT, :UNIT
)";
        }

        public static string InsertHealthCustomer()
        {
            return string.Format(@"INSERT INTO HealthCustomerList (
    CUSTOMERNO,
    CUSTOMERNAME,
    ENGLISHNAME,
    UNIFORMNO,
    CONTACTPERSON,
    ADDRESS,
    TEL,
    FAX,
    EMAIL,
    BUSINESSREGADDRESS,
    TAXOFFICECODE,
    BUSINESSUNION,
    INDUSTRYCATEGORY,
    BUSINESSUNITNAME,
    BUSINESSUNITADDRESS,
    MEDICALMANAGECODE,
    ACCOUNTINGUNIT,
    CUSTOMERNOTE
)
VALUES (
    :CUSTOMERNO,
    :CUSTOMERNAME,
    :ENGLISHNAME,
    :UNIFORMNO,
    :CONTACTPERSON,
    :ADDRESS,
    :TEL,
    :FAX,
    :EMAIL,
    :BUSINESSREGADDRESS,
    :TAXOFFICECODE,
    :BUSINESSUNION,
    :INDUSTRYCATEGORY,
    :BUSINESSUNITNAME,
    :BUSINESSUNITADDRESS,
    :MEDICALMANAGECODE,
    :ACCOUNTINGUNIT,
    :CUSTOMERNOTE
)");
        }

        public static string InsertHealthProjectList()
        {
            string sql = @"
INSERT INTO HealthProjectList (
    PROJECTNO,
    CREATEDATE,
    EDITDATE,
    CHECKDATE,
    CUSTOMERNO,
    SELFCOSTNO,
    YEAR,
    CHECKYEAR,
    PROJECTNAME,
    NOTE,
    COMPAREVAL,
    ENDDATE,
    EXECAREA,
    FLOWNO,
    FLOWNAME,
    QUESTIONNAIRENO,
    QUESTIONNAIRENAME,
    ISPASTHISTORY,
    ISORALCHECK,
    ISONLINEQUESTION,
    STATUS,
    CREATEUSERNO
) VALUES (
    :PROJECTNO,
    :CREATEDATE,
    :EDITDATE,
    :CHECKDATE,
    :CUSTOMERNO,
    :SELFCOSTNO,
    :YEAR,
    :CHECKYEAR,
    :PROJECTNAME,
    :NOTE,
    :COMPAREVAL,
    :ENDDATE,
    :EXECAREA,
    :FLOWNO,
    :FLOWNAME,
    :QUESTIONNAIRENO,
    :QUESTIONNAIRENAME,
    :ISPASTHISTORY,
    :ISORALCHECK,
    :ISONLINEQUESTION,
    :STATUS,
    :CREATEUSERNO
)";

            return sql;

        }

        public static string InsertCheckProjectScheme()
        {
            return @"INSERT INTO CheckProjectScheme (
    PROJECTNO,
    SCHEMENO,
    NAME,
    NOTE,
    PUBLICEXPENSE,
    CHDCTTYPE,
    FIXITEMS
) VALUES (
    :PROJECTNO,
    :SCHEMENO,
    :NAME,
    :NOTE,
    :PUBLICEXPENSE,
    :CHDCTTYPE,
    :FIXITEMS
)";
        }

        public static string InsertCheckProjectSpecialItemTbl()
        {
            return @"INSERT INTO CheckProjectSpecialItemTbl (
    PROJECTNO,
    SPECIALITEMNO,
    PUBLICEXPENSE
) VALUES (
    :PROJECTNO,
    :SPECIALITEMNO,
    :PUBLICEXPENSE
)";
        }
    }

    #endregion

    #region MERGE - 新增或更新語法函式

    public static class MERGE
    {
        public static string UpsertHealthCheckupPatient()
        {
            return @"MERGE INTO HealthCheckupPatient target
USING (
    SELECT
        :PATNO AS PATNO,
        :PATIENTNAME AS PATIENTNAME,
        :IDNUMBER AS IDNUMBER,
        :BIRTHDAY AS BIRTHDAY,
        :AGE AS AGE,
        :MOBILE AS MOBILE,
        :TEL AS TEL,
        :GENDER AS GENDER,
        :CONTACTNAME AS CONTACTNAME,
        :CONTACTTEL AS CONTACTTEL,
        :CONTACTRELATION AS CONTACTRELATION,
        :IS_VIP AS IS_VIP,
        :FAMILYNOTE AS FAMILYNOTE,
        :EMAIL AS EMAIL,
        :SENDINGADDRESS AS SENDINGADDRESS,
        :LIVEADDRESS AS LIVEADDRESS,
        :NOTICENOTE AS NOTICENOTE
    FROM dual
    WHERE :PATNO IS NOT NULL
) source
ON (target.PATNO = source.PATNO)
WHEN MATCHED THEN UPDATE SET
    target.PATIENTNAME = source.PATIENTNAME,
    target.IDNUMBER = source.IDNUMBER,
    target.BIRTHDAY = source.BIRTHDAY,
    target.AGE = source.AGE,
    target.MOBILE = source.MOBILE,
    target.TEL = source.TEL,
    target.GENDER = source.GENDER,
    target.CONTACTNAME = source.CONTACTNAME,
    target.CONTACTTEL = source.CONTACTTEL,
    target.CONTACTRELATION = source.CONTACTRELATION,
    target.IS_VIP = source.IS_VIP,
    target.FAMILYNOTE = source.FAMILYNOTE,
    target.EMAIL = source.EMAIL,
    target.SENDINGADDRESS = source.SENDINGADDRESS,
    target.LIVEADDRESS = source.LIVEADDRESS,
    target.NOTICENOTE = source.NOTICENOTE,
    target.UPDATEDAT = SYSDATE
WHEN NOT MATCHED THEN INSERT (
    PATNO, PATIENTNAME, IDNUMBER, BIRTHDAY, AGE, MOBILE, TEL, GENDER,
    CONTACTNAME, CONTACTTEL, CONTACTRELATION, IS_VIP, FAMILYNOTE, EMAIL,
    SENDINGADDRESS, LIVEADDRESS, NOTICENOTE, CREATEDAT, UPDATEDAT
) VALUES (
    source.PATNO, source.PATIENTNAME, source.IDNUMBER, source.BIRTHDAY, source.AGE,
    source.MOBILE, source.TEL, source.GENDER, source.CONTACTNAME, source.CONTACTTEL,
    source.CONTACTRELATION, source.IS_VIP, source.FAMILYNOTE, source.EMAIL,
    source.SENDINGADDRESS, source.LIVEADDRESS, source.NOTICENOTE, SYSDATE, NULL
)";
        }

        public static string UPDATEHealthCheckupNote()
        {
            return @"MERGE INTO HealthCheckupNOTE target
USING (SELECT :BARCODESTR AS BARCODESTR, :PROJECTNO AS PROJECTNO FROM dual) source
ON (target.BARCODESTR = source.BARCODESTR AND target.PROJECTNO = source.PROJECTNO)
WHEN MATCHED THEN UPDATE SET
    target.FAMILYNOTE    = :FAMILYNOTE,
    target.NOTICENOTE    = :NOTICENOTE,
    target.CURRENTNOTE   = :CURRENTNOTE,
    target.PERMANENTNOTE = :PERMANENTNOTE,
    target.UPDATEDAT     = SYSDATE
WHEN NOT MATCHED THEN INSERT (
    BARCODESTR, PROJECTNO, FAMILYNOTE, NOTICENOTE, CURRENTNOTE, PERMANENTNOTE, CREATEDAT
) VALUES (
    :BARCODESTR, :PROJECTNO, :FAMILYNOTE, :NOTICENOTE, :CURRENTNOTE, :PERMANENTNOTE, SYSDATE
)";
        }

        public static string UPDATEHealthCheckupReserInfo()
        {
            return @"MERGE INTO HealthCheckupReserInfo target
USING (SELECT :BARCODESTR AS BARCODESTR, :PROJECTNO AS PROJECTNO FROM dual) source
ON (target.BARCODESTR = source.BARCODESTR AND target.PROJECTNO = source.PROJECTNO)
WHEN MATCHED THEN UPDATE SET
    target.RESERVE_AREA = :RESERVE_AREA,
    target.RESERVE_DATE = :RESERVE_DATE,
    target.RESERVE_TIME = :RESERVE_TIME,
    target.PARKING      = :PARKING,
    target.LAXATIVE     = :LAXATIVE,
    target.MEAL         = :MEAL,
    target.UPDATED_AT   = SYSDATE
WHEN NOT MATCHED THEN INSERT (
    PROJECTNO, BARCODESTR, RESERVE_AREA, RESERVE_DATE, RESERVE_TIME, PARKING, LAXATIVE, MEAL
) VALUES (
    :PROJECTNO, :BARCODESTR, :RESERVE_AREA, :RESERVE_DATE, :RESERVE_TIME, :PARKING, :LAXATIVE, :MEAL
)";
        }

        public static string UPDATEHealthCheckupPayment()
        {
            return @"MERGE INTO HealthCheckupPayment target
USING (SELECT :BARCODESTR AS BARCODESTR, :PROJECTNO AS PROJECTNO FROM dual) source
ON (target.BARCODESTR = source.BARCODESTR AND target.PROJECTNO = source.PROJECTNO)
WHEN MATCHED THEN UPDATE SET
    target.PACKAGE_AMOUNT  = :PACKAGE_AMOUNT,
    target.EXTRA_AMOUNT    = :EXTRA_AMOUNT,
    target.REMIT_AMOUNT    = :REMIT_AMOUNT,
    target.DISCOUNT_AMOUNT = :DISCOUNT_AMOUNT,
    target.ACCOUNT_AMOUNT  = :ACCOUNT_AMOUNT,
    target.RECEIVED_AMOUNT = :RECEIVED_AMOUNT,
    target.PAYMENT_DATE    = :PAYMENT_DATE,
    target.PAYMENT_METHOD  = :PAYMENT_METHOD,
    target.NOTE            = :NOTE,
    target.UPDATEDAT       = SYSDATE
WHEN NOT MATCHED THEN INSERT (
    PROJECTNO, BARCODESTR, PACKAGE_AMOUNT, EXTRA_AMOUNT, REMIT_AMOUNT, DISCOUNT_AMOUNT,
    ACCOUNT_AMOUNT, RECEIVED_AMOUNT, PAYMENT_DATE, PAYMENT_METHOD, NOTE, CREATEDAT
) VALUES (
    :PROJECTNO, :BARCODESTR, :PACKAGE_AMOUNT, :EXTRA_AMOUNT, :REMIT_AMOUNT, :DISCOUNT_AMOUNT,
    :ACCOUNT_AMOUNT, :RECEIVED_AMOUNT, :PAYMENT_DATE, :PAYMENT_METHOD, :NOTE, SYSDATE
)";
        }

        //    SELECT
        //RESERVEDATE, RESERVETIME, CATEGORY,
        //BARCODESTR, PROJECTNO,
        //PATIENTNAME, IDNUMBER, BIRTHDAY, GENDER, TEL, MOBILE,
        //EMAIL,
        //CATEGORY, ISRESERVE, CUSTOMERNO
        //FROM HealthCustomerList

        public static string UpsertFEMHCheckupList()
        {
            return @"MERGE INTO FEMHCheckupList target
USING
(
    SELECT
        :BARCODESTR AS BARCODESTR,
        :PROJECTNO AS PROJECTNO,
        :INTOP1NO AS INTOP1NO
    FROM dual
) source
ON
(
    target.BARCODESTR = source.BARCODESTR
    AND target.PROJECTNO = source.PROJECTNO
    AND target.INTOP1NO = source.INTOP1NO
)
WHEN MATCHED THEN
    UPDATE SET
        target.CHOP1DATE = :CHOP1DATE,
        target.CHOP1TIME = :CHOP1TIME,
        target.CHOP1ROOM = :CHOP1ROOM,
        target.INTOP1ORDSEQ = :INTOP1ORDSEQ,
        target.CHOP4ORDNO = :CHOP4ORDNO,
        target.CHOP0FIN2 = :CHOP0FIN2,
        target.CHOP0DC = 0,
        target.CDATE = SYSDATE
WHEN NOT MATCHED THEN
    INSERT
    (
        BARCODESTR,
        PROJECTNO,
        CHOP1DATE,
        CHOP1TIME,
        CHOP1ROOM,
        INTOP1NO,
        INTOP1ORDSEQ,
        CHOP4ORDNO,
        CHOP0FIN2,
        CHOP0DC,
        CDATE
    )
    VALUES
    (
        :BARCODESTR,
        :PROJECTNO,
        :CHOP1DATE,
        :CHOP1TIME,
        :CHOP1ROOM,
        :INTOP1NO,
        :INTOP1ORDSEQ,
        :CHOP4ORDNO,
        :CHOP0FIN2,
        0,
        SYSDATE
    )";
        }
    }

    #endregion

    #region OTHER

    public static class OTHER
    {
        internal static string EscapeOracleLiteral(string value) =>
            value.Replace("'", "''", StringComparison.Ordinal);

        internal static string BuildOracleInList(string value, bool upperInvariant = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var items = value
                .Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.Trim('\'', '"', '(', ')'))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .Select(item => upperInvariant ? item.ToUpperInvariant() : item)
                .Select(item => $"'{EscapeOracleLiteral(item)}'");

            return string.Join(",", items);
        }
    }

    #endregion
}



