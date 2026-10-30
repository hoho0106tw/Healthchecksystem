(function () {
 "use strict";

 var page = document.querySelector("[data-inspection-page]");
 if (!page) return;

 var rows = Array.from(page.querySelectorAll("[data-person]"));
 var tabs = Array.from(page.querySelectorAll(".it-tabs [data-status]"));
 var keyword = page.querySelector("#task-keyword");
 var project = page.querySelector("#task-project");
 var empty = page.querySelector(".it-empty");
 var pageSizeSelect = page.querySelector("[data-page-size]");
 var pagination = page.querySelector("[data-pagination]");
 var pageSummary = page.querySelector("[data-page-summary]");
 var workspace = page.querySelector(".it-workspace");
 var listLayoutButtons = Array.from(page.querySelectorAll("[data-list-layout]"));
 var status = "all";
 var currentPage = 1;
 var selectedRow = null;
 var listSortColumn = -1;
 var listSortDirection = 1;
 var listSortButtons = Array.from(page.querySelectorAll("[data-list-sort]"));
 var pageSize = Number.parseInt(pageSizeSelect ? pageSizeSelect.value : "10", 10) || 10;
 var columnHeaders = Array.from(page.querySelectorAll(".it-list thead th")).map(function (cell) {
  return (cell.dataset.columnLabel || cell.textContent).replace(/\s+/g, "").trim();
 });

 function updateTabCounts() {
  tabs.forEach(function (tab) {
   var count = tab.dataset.status === "all"
    ? rows.length
    : rows.filter(function (row) {
     return (row.dataset.status || "").split(" ").indexOf(tab.dataset.status) > -1;
    }).length;
   var badge = tab.querySelector("b");
   if (badge) badge.textContent = count;
  });
 }

 function matchingRows() {
  var query = keyword.value.trim().toLocaleLowerCase("zh-TW");
  var selectedProject = project.value;

  return rows.filter(function (row) {
   var rowStatuses = (row.dataset.status || "").split(" ");
   var searchText = (row.dataset.search || "").toLocaleLowerCase("zh-TW");
   var matchesStatus = status === "all" || rowStatuses.indexOf(status) > -1;
   var matchesKeyword = !query || searchText.indexOf(query) > -1;
   var matchesProject = !selectedProject || (row.dataset.search || "").indexOf(selectedProject) > -1;
   return matchesStatus && matchesKeyword && matchesProject;
  });
 }

 function createPageButton(label, targetPage, options) {
  var settings = options || {};
  var button = document.createElement("button");
  button.type = "button";
  button.textContent = label;
  button.disabled = Boolean(settings.disabled);
  if (settings.label) button.setAttribute("aria-label", settings.label);
  if (settings.active) {
   button.classList.add("active");
   button.setAttribute("aria-current", "page");
  }
  button.addEventListener("click", function () {
   if (button.disabled || currentPage === targetPage) return;
   currentPage = targetPage;
   renderList();
  });
  return button;
 }

 function renderPagination(totalPages) {
  if (!pagination) return;
  var controls = [createPageButton("‹", Math.max(1, currentPage - 1), {
   disabled: currentPage === 1,
   label: "上一頁"
  })];

  for (var pageNumber = 1; pageNumber <= totalPages; pageNumber += 1) {
   controls.push(createPageButton(String(pageNumber), pageNumber, {
    active: pageNumber === currentPage,
    label: "第 " + pageNumber + " 頁"
   }));
  }

  controls.push(createPageButton("›", Math.min(totalPages, currentPage + 1), {
   disabled: currentPage === totalPages,
   label: "下一頁"
  }));
  pagination.replaceChildren.apply(pagination, controls);
 }

 function renderList() {
  var matched = matchingRows();
  if(listSortColumn>=0){
   matched.sort(function(left,right){
    return cellText(left,listSortColumn).localeCompare(cellText(right,listSortColumn),"zh-TW",{numeric:true,sensitivity:"base"})*listSortDirection;
   });
   matched.forEach(function(row){row.parentNode.appendChild(row);});
  }
  var totalPages = Math.max(1, Math.ceil(matched.length / pageSize));
  if (currentPage > totalPages) currentPage = totalPages;

  var firstIndex = (currentPage - 1) * pageSize;
  var visibleRows = matched.slice(firstIndex, firstIndex + pageSize);
  rows.forEach(function (row) { row.hidden = visibleRows.indexOf(row) === -1; });

  empty.hidden = matched.length !== 0;
  if (pageSummary) {
   pageSummary.textContent = "共 " + matched.length + " 筆";
  }
  renderPagination(totalPages);
  if (visibleRows.indexOf(selectedRow) === -1) {
   selectRow(visibleRows[0] || null);
  }
 }

 function cellText(row, index) {
  var cell = row.cells[index];
  return cell ? cell.textContent.trim() : "";
 }

 function sortList(columnIndex) {
  listSortDirection = listSortColumn === columnIndex ? -listSortDirection : 1;
  listSortColumn = columnIndex;
  listSortButtons.forEach(function(button){
   var active=Number(button.dataset.listSort)===columnIndex;
   button.closest("th").setAttribute("aria-sort",active?(listSortDirection===1?"ascending":"descending"):"none");
  });
  currentPage=1;
  renderList();
 }

 function cellTextByHeader(row, headerNames, fallbackIndex) {
  var index = -1;
  headerNames.some(function (headerName) {
   index = columnHeaders.indexOf(headerName);
   return index > -1;
  });
  return cellText(row, index > -1 ? index : fallbackIndex);
 }

 function renderFlow(row) {
  var flow=page.querySelector("[data-inspection-flow]");
  flow.replaceChildren();
  var steps=[];
  try{steps=row?JSON.parse(row.dataset.flow||"[]"):[];}catch(_){steps=[];}
  steps.forEach(function(step,index){
   var item=document.createElement("li"),icon=document.createElement("i"),name=document.createElement("b"),state=document.createElement("em");
   item.className=step.state;
   icon.setAttribute("aria-hidden","true");
   icon.textContent=step.state==="done"?"✓":step.state==="active"?"●":String(index+1);
   name.textContent=step.name;
   state.textContent=step.status+(step.time?" · "+step.time:"");
   if(step.state==="active")item.setAttribute("aria-current","step");
   item.append(icon,name,state);
   flow.appendChild(item);
  });
  if(!steps.length){var empty=document.createElement("li");empty.textContent="請先選擇受檢者";flow.appendChild(empty);}
  var percent=row?Number.parseFloat(row.dataset.progress)||0:0;
  percent=Math.min(100,Math.max(0,percent));
  page.querySelector(".it-person .state label strong").textContent=percent+"%";
  var bar=page.querySelector(".it-person .state .wide");
  bar.setAttribute("role","progressbar");
  bar.setAttribute("aria-label","健檢完成度");
  bar.setAttribute("aria-valuemin","0");bar.setAttribute("aria-valuemax","100");bar.setAttribute("aria-valuenow",String(percent));
  bar.classList.toggle("done",percent===100);
  bar.querySelector("i").style.width=percent+"%";
 }

 function selectRow(row) {
  selectedRow = row;
  rows.forEach(function (item) { item.classList.toggle("selected", item === row); });

  if (!row) {
   Array.from(page.querySelectorAll(".it-person .name h2, .it-person .name > span, .it-person dl dd, .it-person .state .badge, .it-person .state label strong, .it-subview-person b, .it-subview-person div span, .it-subview-person > .badge")).forEach(function (element) {
    element.textContent = "";
   });
   var emptyProgressBar = page.querySelector(".it-person .state .wide i");
   if (emptyProgressBar) emptyProgressBar.style.width = "0%";
   var emptyAlert = page.querySelector(".it-detail-main > .it-alert");
   if (emptyAlert) emptyAlert.hidden = true;
   renderFlow(null);
   refreshCurrentView();
   return;
  }

  var name = cellTextByHeader(row, ["姓名"], 1);
  var patientNo = cellTextByHeader(row, ["病歷號", "病歷號碼"], -1);
  var barcode = row.dataset.barcode || cellTextByHeader(row, ["條碼"], 3);
  var projectName = cellTextByHeader(row, ["專案"], 2);
  var statusIndex = columnHeaders.indexOf("狀態");
  var statusCell = row.cells[statusIndex > -1 ? statusIndex : 0];
  var sourceBadge = statusCell ? statusCell.querySelector(".badge") : null;
  var statusText = sourceBadge ? sourceBadge.textContent.trim() : cellTextByHeader(row, ["狀態"], 0);
  var statusTone = sourceBadge ? Array.from(sourceBadge.classList).find(function (name) { return name !== "badge"; }) : "waiting";

  page.querySelector(".it-person .name h2").textContent = name;
  page.querySelector(".it-person .name > span").textContent = row.dataset.personMeta || "";
  var detailValues = page.querySelectorAll(".it-person dl dd");
  if (detailValues[0]) detailValues[0].textContent = patientNo;
  if (detailValues[1]) detailValues[1].textContent = barcode;
  if (detailValues[2]) detailValues[2].textContent = projectName;

  var detailStatus = page.querySelector(".it-person .state .badge:first-child");
  if (detailStatus) {
   detailStatus.className = "badge " + (statusTone || "waiting");
   detailStatus.textContent = statusText;
  }
  renderFlow(row);

  var subviewName = page.querySelector(".it-subview-person b");
  var subviewMeta = page.querySelector(".it-subview-person div span");
  var subviewStatus = page.querySelector(".it-subview-person > .badge");
  if (subviewName) subviewName.textContent = name;
  if (subviewMeta) subviewMeta.textContent = "病歷號 " + patientNo + "　條碼 " + barcode;
  if (subviewStatus) {
   subviewStatus.className = "badge " + (statusTone || "waiting");
   subviewStatus.textContent = statusText;
  }

  refreshCurrentView();
 }

 listSortButtons.forEach(function(button){
  button.addEventListener("click",function(){sortList(Number(button.dataset.listSort));});
 });
 tabs.forEach(function (tab) {
  tab.addEventListener("click", function () {
   status = tab.dataset.status;
   currentPage = 1;
   tabs.forEach(function (item) { item.classList.toggle("active", item === tab); });
   renderList();
  });
 });
 keyword.addEventListener("input", function () { currentPage = 1; renderList(); });
 project.addEventListener("change", function () { currentPage = 1; renderList(); });
 if (pageSizeSelect) {
  pageSizeSelect.addEventListener("change", function () {
   pageSize = Number.parseInt(pageSizeSelect.value, 10) || 10;
   currentPage = 1;
   renderList();
  });
 }
 listLayoutButtons.forEach(function (button) {
  button.addEventListener("click", function () {
   var layout = button.dataset.listLayout || "compact";
   workspace.classList.remove("list-layout-hidden","list-layout-compact","list-layout-expanded");
   workspace.classList.add("list-layout-" + layout);
   listLayoutButtons.forEach(function (item) { item.classList.toggle("active", item === button); });
  });
 });
 rows.forEach(function (row) {
  row.addEventListener("click", function () { selectRow(row); });
  row.addEventListener("keydown", function (event) {
   if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    selectRow(row);
   }
  });
 });

 var views={
  lab:{icon:"♜",title:"Lab 檢驗",description:"查看檢驗結果、參考值與報告狀態。",total:0,complete:0,alert:0,rows:[]},
  image:{icon:"",title:"影像報告",description:"查看影像檢查結果與醫師判讀。",total:0,complete:0,alert:0,rows:[]},
  special:{icon:"✚",title:"特檢報告",description:"查看特殊檢查報告與判讀結果。",emptyMessage:"尚無特檢報告資料。",total:0,complete:0,alert:0,rows:[]},
  ecg:{icon:"⌁",title:"心電圖",description:"查看心電圖檢查結果與醫師判讀。",emptyMessage:"尚無心電圖資料。",total:0,complete:0,alert:0,rows:[]},
  measure:{icon:"●",title:"身體測量",description:"查看身高、體重、血壓及身體組成。",emptyMessage:"",total:"",complete:"",alert:"",rows:[]},
  survey:{icon:"▣",title:"問卷資料",description:"查看健康史、生活型態與自覺症狀問卷。",emptyMessage:"",total:"",complete:"",alert:"",rows:[]},
  doctor:{icon:"♧",title:"醫師總評",description:"查看問診紀錄、處置建議與總評進度。",emptyMessage:"",total:"",complete:"",alert:"",rows:[]}
 };
 var overview=page.querySelector("[data-detail-overview]"),subview=page.querySelector("[data-detail-subview]"),nav=Array.from(page.querySelectorAll(".it-side-nav [data-target]"));
 function statusClass(value){if(value==="危險值"||value==="偏高"||value==="建議複檢")return "alert";if(value==="尚未完成"||value==="進行中")return "pending";return ""}
 var labRequest = null;
 var labPageSize = 10;
 var labCurrentPage = 1;
 var labData = null;
 var labPaginationFooter = subview.querySelector("[data-lab-pagination-footer]");
 var labPagination = subview.querySelector("[data-lab-pagination]");
 var labPageSummary = subview.querySelector("[data-lab-page-summary]");
 function setHeaders(labels) {
  var headerRow = subview.querySelector("[data-view-headers]");
  headerRow.textContent = "";
  labels.forEach(function (label) {
   var th = document.createElement("th");
   th.textContent = label;
   headerRow.appendChild(th);
  });
 }
 function setStats(total, complete, alert) {
  subview.querySelector("[data-view-total]").textContent=total;
  subview.querySelector("[data-view-complete]").textContent=complete;
  subview.querySelector("[data-view-alert]").textContent=alert;
 }
 function showTableMessage(message, columnCount) {
  var body=subview.querySelector("[data-view-rows]");
  var tr=document.createElement("tr"),td=document.createElement("td");
  body.textContent="";
  td.className="it-table-message";
  td.colSpan=columnCount;
  td.textContent=message;
  tr.appendChild(td);
  body.appendChild(tr);
 }
 function renderStandardRows(data) {
  if (labPaginationFooter) labPaginationFooter.hidden = true;
  setHeaders(["項目","結果／內容","狀態","更新時間"]);
  setStats(data.total,data.complete,data.alert);
  var body=subview.querySelector("[data-view-rows]");body.textContent="";
  if(data.rows.length===0){showTableMessage(data.emptyMessage === "" ? "" : data.emptyMessage||"尚無資料。",4);return;}
  data.rows.forEach(function(item){var tr=document.createElement("tr");item.forEach(function(value,index){var td=document.createElement("td");if(index===2){var badge=document.createElement("span");badge.className="it-row-status "+statusClass(value);badge.textContent=value;td.appendChild(badge)}else td.textContent=value;tr.appendChild(td)});body.appendChild(tr)});
 }
 function createLabPageButton(label, targetPage, options) {
  var settings = options || {};
  var button = document.createElement("button");
  button.type = "button";
  button.textContent = label;
  button.disabled = Boolean(settings.disabled);
  if (settings.label) button.setAttribute("aria-label", settings.label);
  if (settings.active) {
   button.classList.add("active");
   button.setAttribute("aria-current", "page");
  }
  button.addEventListener("click", function () {
   if (button.disabled || labCurrentPage === targetPage) return;
   labCurrentPage = targetPage;
   renderLabPage();
  });
  return button;
 }
 function renderLabPagination(totalPages) {
  if (!labPagination) return;
  var controls=[createLabPageButton("‹",Math.max(1,labCurrentPage-1),{disabled:labCurrentPage===1,label:"上一頁"})];
  for(var pageNumber=1;pageNumber<=totalPages;pageNumber+=1){
   controls.push(createLabPageButton(String(pageNumber),pageNumber,{active:pageNumber===labCurrentPage,label:"第 "+pageNumber+" 頁"}));
  }
  controls.push(createLabPageButton("›",Math.min(totalPages,labCurrentPage+1),{disabled:labCurrentPage===totalPages,label:"下一頁"}));
  labPagination.replaceChildren.apply(labPagination,controls);
 }
 function renderLabPage() {
  var rowsToRender=labData && Array.isArray(labData.rows)?labData.rows:[];
  var body=subview.querySelector("[data-view-rows]");body.textContent="";
  if (labPaginationFooter) labPaginationFooter.hidden = false;
  if (rowsToRender.length === 0) {
   if(labPageSummary)labPageSummary.textContent="顯示第 0 - 0 筆，共 0 筆";
   renderLabPagination(1);
   showTableMessage("查無 Lab 檢驗資料。",6);
   return;
  }
  var totalPages=Math.max(1,Math.ceil(rowsToRender.length/labPageSize));
  if(labCurrentPage>totalPages)labCurrentPage=totalPages;
  var firstIndex=(labCurrentPage-1)*labPageSize;
  var visibleRows=rowsToRender.slice(firstIndex,firstIndex+labPageSize);
  if(labPageSummary)labPageSummary.textContent="顯示第 "+(firstIndex+1)+" - "+(firstIndex+visibleRows.length)+" 筆，共 "+rowsToRender.length+" 筆";
  renderLabPagination(totalPages);
  visibleRows.forEach(function (item) {
   var values=[item.receiptDate,item.reportDate,item.labName,item.value || "—",item.referenceValue,item.statusName];
   var tr=document.createElement("tr");
   values.forEach(function (value,index) {
    var td=document.createElement("td");
    if(index===3){
     td.className="it-lab-value-cell"+(item.isAbnormal?" it-lab-abnormal":"");
     var valueText=document.createElement("span");
     valueText.className="it-lab-value";
     valueText.textContent=value||"—";
     td.appendChild(valueText);
     if(item.isCritical){
      var criticalBadge=document.createElement("span");
      criticalBadge.className="badge danger it-critical-badge";
      criticalBadge.textContent="危急";
      td.appendChild(criticalBadge);
     }
    }else if(index===5){var badge=document.createElement("span");badge.className="it-row-status "+(item.statusName==="申請中"?"pending":"");badge.textContent=value||"—";td.appendChild(badge)}else td.textContent=value||"—";
    tr.appendChild(td);
   });
   body.appendChild(tr);
  });
 }
 function renderLabRows(data) {
  labData=data;
  labCurrentPage=1;
  setStats(data.total || 0,data.complete || 0,data.alert || 0);
  renderLabPage();
 }
 function resetOverviewLab(message) {
  page.querySelector("[data-overview-lab-total]").textContent="";
  page.querySelector("[data-overview-lab-count]").textContent="";
  var badge=page.querySelector("[data-overview-lab-badge]");
  badge.textContent="";
  badge.hidden=true;
  var alert=page.querySelector("[data-overview-lab-alert]");
  alert.textContent="";
  alert.hidden=true;
  page.querySelector(".it-stations [data-target='lab']").classList.remove("danger");
  var body=page.querySelector("[data-overview-lab-rows]");
  body.textContent="";
  if(message){
   var tr=document.createElement("tr"),td=document.createElement("td");
   td.colSpan=5;
   td.className="it-table-message";
   td.textContent=message;
   tr.appendChild(td);
   body.appendChild(tr);
  }
 }
 function renderOverviewLab(data) {
  resetOverviewLab();
  var results=Array.isArray(data.rows)?data.rows:[];
  var abnormal=results.filter(function(item){return item.isAbnormal;});
  var critical=results.filter(function(item){return item.isCritical;});
  var summary=critical.length?"LAB "+critical.length+" 項危急 / "+abnormal.length+" 項異常":"LAB "+abnormal.length+" 項異常";
  page.querySelector("[data-overview-lab-total]").textContent=results.length+" 項";
  page.querySelector("[data-overview-lab-count]").textContent=summary;
  page.querySelector(".it-stations [data-target='lab']").classList.toggle("danger",abnormal.length>0);
  var badge=page.querySelector("[data-overview-lab-badge]");
  badge.textContent=summary;
  badge.hidden=false;
  badge.classList.toggle("is-clear",abnormal.length===0);
  var body=page.querySelector("[data-overview-lab-rows]");
  abnormal.forEach(function(item){
   var tr=document.createElement("tr");
   ["LAB",item.labName,item.value,item.referenceValue,item.isCritical?"危急":"異常"].forEach(function(value,index){
    var td=document.createElement("td");
    td.textContent=value||"";
    if(index===2 || (index===4 && item.isCritical))td.className="red";
    tr.appendChild(td);
   });
   body.appendChild(tr);
  });
  if(abnormal.length){
   var alert=page.querySelector("[data-overview-lab-alert]");
   alert.textContent=summary+"："+abnormal.map(function(item){return item.labName+(item.isCritical?"（危急）":"");}).join("、");
   alert.hidden=false;
  }
 }
 function loadLabResults(forOverview) {
  if(labRequest)labRequest.abort();
  labData=null;
  resetOverviewLab();
  function showMessage(message){
   resetOverviewLab(message);
   if(!forOverview)showTableMessage(message,6);
  }
  if(!forOverview){
   setHeaders(["簽收日期","報告日期","檢查項目","檢查值","參考值","報告狀態"]);
   setStats(0,0,0);
  }
  if(labPaginationFooter)labPaginationFooter.hidden=true;
  if (!selectedRow) {showMessage("請先選擇檢查報告。");return;}
  var keys={chop1Date:selectedRow.dataset.chop1Date,chop1Time:selectedRow.dataset.chop1Time,chop1Room:selectedRow.dataset.chop1Room,intop1No:selectedRow.dataset.intop1No};
  if(!keys.chop1Date||!keys.chop1Time||!keys.chop1Room||!keys.intop1No){showMessage("此檢查報告缺少 Lab 查詢所需的四個 key。");return;}
  var request=labRequest=new AbortController();
  showMessage("Lab 檢驗資料載入中…");
  var parameters=new URLSearchParams(keys);
  fetch(page.dataset.labUrl+"?"+parameters.toString(),{headers:{Accept:"application/json"},cache:"no-store",signal:request.signal})
   .then(function(response){return response.json().catch(function(){return {};}).then(function(payload){if(!response.ok)throw new Error(payload.message||"無法載入 Lab 檢驗資料。");return payload;});})
   .then(function(data){if(!request.signal.aborted && labRequest===request){renderOverviewLab(data);if(!forOverview)renderLabRows(data);}})
   .catch(function(error){if(!request.signal.aborted && labRequest===request && error.name!=="AbortError")showMessage(error.message||"無法載入 Lab 檢驗資料。");});
 }
 var imageRequest = null;
 function reportText(value) {
  if (!value) return "—";
  var documentFragment = new DOMParser().parseFromString(String(value), "text/html");
  Array.from(documentFragment.body.querySelectorAll("br")).forEach(function (lineBreak) {
   lineBreak.replaceWith(documentFragment.createTextNode("\n"));
  });
  return documentFragment.body.textContent.trim() || "—";
 }
 var reportTabsObserver=null;
 function renderImageReports(data, isSpecial, isEcg) {
  if(reportTabsObserver){reportTabsObserver.disconnect();reportTabsObserver=null;}
  var reportLabel=isEcg?"心電圖":isSpecial?"特檢報告":"影像報告";
  if (labPaginationFooter) labPaginationFooter.hidden = true;
  setHeaders([reportLabel]);
  setStats(data.total || 0,data.complete || 0,data.alert || 0);
  var body=subview.querySelector("[data-view-rows]");body.textContent="";
  var reportRows=Array.isArray(data.rows)?data.rows:[];
  if(reportRows.length===0){showTableMessage("查無"+reportLabel+"資料。",1);return;}
  var tr=document.createElement("tr");
  var td=document.createElement("td");
  var viewer=document.createElement("div");
  var tabList=document.createElement("div");
  var panels=[];
  viewer.className="it-image-report-viewer";
  tabList.className="it-image-report-tabs";
  tabList.setAttribute("role","tablist");
  tabList.setAttribute("aria-label",reportLabel+"列表");
  reportRows.forEach(function(item,index){
   var tab=document.createElement("button");
   var report=document.createElement("article");
   var findingsSection=document.createElement("section");
   var findingsTitle=document.createElement("h3");
   var findingsText=document.createElement("p");
   var impressionSection=document.createElement("section");
   var impressionTitle=document.createElement("h3");
   var impressionText=document.createElement("p");
   var tabId="image-report-tab-"+index;
   var panelId="image-report-panel-"+index;
   tab.type="button";
   tab.className="it-image-report-tab"+(index===0?" active":"");
   tab.id=tabId;
   tab.setAttribute("role","tab");
   tab.setAttribute("aria-selected",index===0?"true":"false");
   tab.setAttribute("aria-controls",panelId);
   [["報告狀態",item.statusName],["報告醫師",item.modifyDoctor],["報告日期",item.reportDate]].forEach(function(meta){
    var line=document.createElement("span");
    var label=document.createElement("b");
    var value=document.createElement("em");
    label.textContent=meta[0];
    value.textContent=isEcg?"":meta[1]||"—";
    line.appendChild(label);
    line.appendChild(value);
    tab.appendChild(line);
   });
   report.className="it-image-report"+(isSpecial?" it-special-report":"");
   report.id=panelId;
   report.setAttribute("role","tabpanel");
   report.setAttribute("aria-labelledby",tabId);
   report.hidden=index!==0;
   findingsTitle.textContent=isSpecial?"報告":"Findings";
   findingsText.textContent=reportText(isSpecial?item.content:item.findings);
   impressionTitle.textContent="Impression";
   impressionText.textContent=reportText(item.impression);
   function addReportZoom(section,heading){
    section.className="it-report-zoom-section";
    section.tabIndex=0;
    section.title="";
    var reportHeader=document.createElement("div");
    var zoomControls=document.createElement("div");
    var zoomSelect=document.createElement("select");
    zoomControls.className="it-report-zoom-controls";
    zoomControls.setAttribute("role","group");
    zoomControls.setAttribute("aria-label","報告縮放控制");
    zoomSelect.className="it-report-zoom-select";
    zoomSelect.title="報告縮放比例（預設不放大）";
    zoomSelect.setAttribute("aria-label","報告縮放比例");
    [0,60,80,100,120,140,160,180,200].forEach(function(percent){
     var option=document.createElement("option");
     option.value=String(percent);
     option.textContent=percent===0?"預設":percent+"%";
     option.selected=percent===0;
     zoomSelect.appendChild(option);
    });
    zoomSelect.addEventListener("change",function(){
     var percent=Number(zoomSelect.value);
     section.classList.toggle("is-magnifier-enabled",percent>0);
     if(percent>0)section.style.setProperty("--it-report-zoom-font-size",(20*percent/100)+"px");
     else section.style.removeProperty("--it-report-zoom-font-size");
     section.title=percent>0?"滑鼠移入可增高並放大報告，移出恢復原大小":"";
    });
    reportHeader.className="it-special-report-header";
    reportHeader.appendChild(heading);
    zoomControls.appendChild(zoomSelect);
    reportHeader.appendChild(zoomControls);
    section.appendChild(reportHeader);
   }
   addReportZoom(findingsSection,findingsTitle);
   if(!isSpecial)addReportZoom(impressionSection,impressionTitle);
   findingsSection.appendChild(findingsText);
   impressionSection.appendChild(impressionText);
   report.appendChild(findingsSection);
   if(!isSpecial)report.appendChild(impressionSection);
   tab.addEventListener("click",function(){
    Array.from(tabList.querySelectorAll("[role='tab']")).forEach(function(itemTab){
     var active=itemTab===tab;
     itemTab.classList.toggle("active",active);
     itemTab.setAttribute("aria-selected",active?"true":"false");
    });
    panels.forEach(function(panel){panel.hidden=panel!==report;});
   });
   tabList.appendChild(tab);
   panels.push(report);
  });
  viewer.appendChild(tabList);
  panels.forEach(function(panel){viewer.appendChild(panel);});
  td.appendChild(viewer);
  tr.appendChild(td);
  body.appendChild(tr);
  {
   var pager=document.createElement("div");
   var previous=document.createElement("button");
   var next=document.createElement("button");
   var tabs=Array.from(tabList.children);
   var pageIndex=0;
   var pageSize=1;
   pager.className="it-special-tabs-pager";
   tabList.classList.add("it-special-tabs-single-row");
   previous.type=next.type="button";
   previous.textContent="<<";
   next.textContent=">>";
   previous.title="上一頁";
   next.title="下一頁";
   previous.setAttribute("aria-label","報告索引上一頁");
   next.setAttribute("aria-label","報告索引下一頁");
   previous.className=next.className="it-report-page-button";
   viewer.insertBefore(pager,tabList);
   pager.appendChild(previous);
   pager.appendChild(tabList);
   pager.appendChild(next);
   function showReportPage(){
    var pageCount=Math.ceil(tabs.length/pageSize);
    pageIndex=Math.min(pageIndex,pageCount-1);
    tabs.forEach(function(tab,index){tab.hidden=index<pageIndex*pageSize||index>=(pageIndex+1)*pageSize;});
    previous.disabled=pageIndex===0;
    next.disabled=pageIndex===pageCount-1;
   }
   function resizeReportPages(){
    if(!tabList.clientWidth)return;
    var size=Math.max(1,Math.floor((tabList.clientWidth+8)/218));
    if(size!==pageSize){
     var selected=tabs.findIndex(function(tab){return tab.getAttribute("aria-selected")==="true";});
     pageSize=size;
     pageIndex=Math.floor(Math.max(0,selected)/pageSize);
    }
    showReportPage();
   }
   previous.addEventListener("click",function(){pageIndex--;showReportPage();});
   next.addEventListener("click",function(){pageIndex++;showReportPage();});
   resizeReportPages();
   reportTabsObserver=new ResizeObserver(resizeReportPages);
   reportTabsObserver.observe(tabList);
  }
 }
 function loadImageReports(isSpecial, isEcg) {
  var reportLabel=isEcg?"心電圖":isSpecial?"特檢報告":"影像報告";
  if(imageRequest)imageRequest.abort();
  setHeaders([reportLabel]);
  setStats(0,0,0);
  if(labPaginationFooter)labPaginationFooter.hidden=true;
  if(!selectedRow){showTableMessage("請先選擇檢查報告。",1);return;}
  var keys={chop1Date:selectedRow.dataset.chop1Date,chop1Time:selectedRow.dataset.chop1Time,chop1Room:selectedRow.dataset.chop1Room,intop1No:selectedRow.dataset.intop1No};
  if(!keys.chop1Date||!keys.chop1Time||!keys.chop1Room||!keys.intop1No){showTableMessage("此檢查報告缺少"+reportLabel+"查詢所需的四個 key。",1);return;}
  var request=imageRequest=new AbortController();
  showTableMessage(reportLabel+"資料載入中…",1);
  var parameters=new URLSearchParams(keys);
  fetch((isEcg?page.dataset.ecgUrl:isSpecial?page.dataset.specialUrl:page.dataset.imageUrl)+"?"+parameters.toString(),{headers:{Accept:"application/json"},cache:"no-store",signal:request.signal})
   .then(function(response){return response.json().catch(function(){return {};}).then(function(payload){if(!response.ok)throw new Error(payload.message||"無法載入"+reportLabel+"資料。");return payload;});})
   .then(function(data){if(!request.signal.aborted && imageRequest===request)renderImageReports(data,isSpecial,isEcg);})
   .catch(function(error){if(!request.signal.aborted && error.name!=="AbortError")showTableMessage(error.message||"無法載入"+reportLabel+"資料。",1);});
 }
 var activeView = "overview";
 function refreshCurrentView() {
  if(reportTabsObserver){reportTabsObserver.disconnect();reportTabsObserver=null;}
  if(labRequest)labRequest.abort();
  if(imageRequest)imageRequest.abort();
  resetOverviewLab();
  if(activeView==="overview"){loadLabResults(true);return;}
  if(subview.hidden)return;
  if(activeView==="lab")loadLabResults();
  else if(activeView==="image")loadImageReports();
  else if(activeView==="special")loadImageReports(true);
  else if(activeView==="ecg")loadImageReports(true,true);
  else if(views[activeView])renderStandardRows(views[activeView]);
 }
 function switchView(target) {
  if(target!=="overview" && !views[target])return;
  activeView=target;
  nav.forEach(function(x){x.classList.toggle("active",x.dataset.target===target);});
  subview.classList.toggle("is-lab-view",target==="lab");
  subview.classList.toggle("is-image-view",target==="image"||target==="special"||target==="ecg");
  overview.hidden=target!=="overview";
  subview.hidden=target==="overview";
  if(!subview.hidden){
   var data=views[target];
   subview.querySelector("[data-view-icon]").textContent=data.icon;
   subview.querySelector("[data-view-title]").textContent=data.title;
   subview.querySelector("[data-view-description]").textContent=data.description;
  }
  refreshCurrentView();
 }
 Array.from(page.querySelectorAll("[data-target]")).forEach(function(button){button.addEventListener("click",function(){switchView(button.dataset.target)})});page.querySelector("[data-back-overview]").addEventListener("click",function(){switchView("overview")});

 updateTabCounts();
 renderList();
})();
