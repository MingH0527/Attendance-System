(()=>{"use strict";
const HOURS=[9,10,11,12,13,14,15,16];
const DAYS={School:[1,2,3,4,5],Tuition:[6,0]};
const DAY_NAME={0:"Sunday",1:"Monday",2:"Tuesday",3:"Wednesday",4:"Thursday",5:"Friday",6:"Saturday"};
const S={bucket:"current",dashboard:null,term:null,program:null,cls:null,type:"School",mode:"view",entries:[],assignments:[],rooms:[],holidays:[]};
const $=id=>document.getElementById(id);
const esc=value=>{const d=document.createElement("div");d.textContent=value??"";return d.innerHTML};

document.addEventListener("DOMContentLoaded",init);

async function init(){bind();await dashboard();}

function bind(){
 document.querySelectorAll("[data-status]").forEach(b=>b.onclick=async()=>{
  document.querySelectorAll("[data-status]").forEach(x=>x.classList.remove("active"));
  b.classList.add("active");S.bucket=b.dataset.status;showDashboard();await dashboard();
 });
 $("backDashboard").onclick=showDashboard;
 $("backProgram").onclick=()=>showProgram(S.term,S.program);
 $("schoolBtn").onclick=()=>switchType("School");
 $("tuitionBtn").onclick=()=>switchType("Tuition");
 $("viewBtn").onclick=()=>setMode("view");
 $("editBtn").onclick=()=>setMode("edit");
 $("cancelLessonBtn").onclick=closeLessonModal;
 $("lessonForm").onsubmit=saveLesson;
 $("deleteLessonBtn").onclick=deleteLesson;
 $("assignmentSearch").oninput=renderAssignments;
 $("newSemesterBtn").onclick=()=>openSemesterModal();
 $("closeSemesterBtn").onclick=$("cancelSemesterBtn").onclick=closeSemesterModal;
 $("semesterForm").onsubmit=createSemester;
 $("cancelForceActiveBtn").onclick=cancelForceActiveConfirmation;
 $("confirmForceActiveBtn").onclick=confirmForceActiveCreation;
 $("holidayForm").onsubmit=addHoliday;
 $("holidayTerm").onchange=loadHolidayList;
 $("loadMalaysiaHolidaysBtn").onclick=loadMalaysiaHolidays;
 $("chooseHolidayFile").onclick=()=>$("holidayFile").click();
 $("holidayFile").onchange=()=>{const f=$("holidayFile").files?.[0];if(f)importHolidayFile(f)};
 const dz=$("holidayDropZone");
 dz.onclick=e=>{if(e.target.tagName!=="BUTTON")$("holidayFile").click()};
 dz.ondragover=e=>{e.preventDefault();dz.classList.add("drag-over")};
 dz.ondragleave=()=>dz.classList.remove("drag-over");
 dz.ondrop=e=>{e.preventDefault();dz.classList.remove("drag-over");const f=e.dataTransfer.files?.[0];if(f)importHolidayFile(f)};
 ["Pdf","Excel","Csv"].forEach(format=>$("export"+format).onclick=()=>{
  const date=$("weekDate").value;
  location.href=`/Timetable/Export?termId=${encodeURIComponent(S.term.termId)}&classId=${encodeURIComponent(S.cls.classId)}&classType=${S.type}&format=${format.toLowerCase()}${date?`&weekDate=${date}`:""}`;
 });
}

async function dashboard(){
 const r=await fetch("/Timetable/Dashboard");
 if(!r.ok){$("termCards").innerHTML="<div class='tt-error'>Unable to load timetable dashboard.</div>";return;}
 S.dashboard=await r.json();
 const host=$("termCards");host.innerHTML="";
 const holidayMode=S.bucket==="holiday";
 $("holidayHistoryPanel").hidden=!holidayMode;
 host.hidden=holidayMode;
 if(holidayMode){await prepareHolidayHistory();return;}
 const terms=S.bucket==="current"?S.dashboard.current:S.dashboard.completed;
 $("newSemesterBtn").disabled=(S.dashboard.current||[]).length>=2;
 $("newSemesterBtn").title=$("newSemesterBtn").disabled?"Maximum two semesters can be kept on the timetable page.":"Create a new semester";
 if(!terms.length){host.innerHTML="<div class='tt-empty'>No academic terms in this section.</div>";return;}
 terms.forEach(t=>{
  const card=document.createElement("article");card.className=`tt-term-card ${t.status==="Warning"?"tt-term-warning":""}`;
  const restoredStatusChoice=t.isRestored
   ? `<div class="tt-status-choice" aria-label="Restored semester status">
        <button type="button" class="tt-status-choice-btn selected" disabled>Incoming</button>
        <button type="button" data-term-action="activate-restored" class="tt-status-choice-btn active-choice">Set Active</button>
      </div>`
   : "";
  const actions=S.bucket==="current"
   ? `<div class="tt-term-actions">${t.status==="Incoming"?'<button type="button" data-term-action="edit" class="tt-btn-secondary">Edit Dates</button>':''}${restoredStatusChoice}<button type="button" data-term-action="archive" class="tt-btn-secondary">Move to History</button></div>`
   : `<div class="tt-term-actions"><button type="button" data-term-action="restore" class="tt-btn-secondary">Restore</button></div>`;
  card.innerHTML=`<header><div><h3>${esc(t.name)}</h3><small>${dateText(t.startDate)} – ${dateText(t.endDate)}</small></div><div class="tt-term-status-wrap"><span class="tt-pill ${esc(t.status.toLowerCase())}">${esc(t.status)}</span>${actions}</div></header><div class="tt-program-grid"></div>`;
  card.querySelectorAll("[data-term-action]").forEach(b=>b.onclick=()=>handleTermAction(b.dataset.termAction,t));
  const grid=card.querySelector(".tt-program-grid");
  t.programs.forEach(p=>{
   const pc=document.createElement("div");pc.className="tt-program-card";
   pc.innerHTML=`<strong>${esc(p.programCode)}</strong><small>${esc(p.programName)}</small><span>${p.classes.length} class${p.classes.length===1?"":"es"}</span><button type="button">Open Program →</button>`;
   pc.querySelector("button").onclick=()=>showProgram(t,p);
   grid.appendChild(pc);
  });
  host.appendChild(card);
 });
}

function showProgram(term,program){
 S.term=term;S.program=program;
 $("dashboardView").hidden=true;$("workspaceView").hidden=true;$("programView").hidden=false;
 $("backDashboard").hidden=false;$("backProgram").hidden=true;
 $("programBreadcrumb").innerHTML=`<strong>${esc(term.name)}</strong> · ${esc(program.programCode)} <span>${esc(program.programName)}</span>`;
 const host=$("programClassCards");host.innerHTML="";
 if(!program.classes.length){host.innerHTML="<div class='tt-empty'>No classes registered for this program.</div>";return;}
 program.classes.forEach(c=>{
  const x=document.createElement("div");x.className="tt-class-card";
  const editable=term.status==="Active"||term.status==="Incoming";
  x.innerHTML=`<strong>${esc(c.className)}</strong><div><button data-open="School:view">School View</button>${editable?'<button data-open="School:edit">School Edit</button>':''}</div><div><button data-open="Tuition:view">Tuition View</button>${editable?'<button data-open="Tuition:edit">Tuition Edit</button>':''}</div>`;
  x.querySelectorAll("[data-open]").forEach(b=>b.onclick=()=>{const [type,mode]=b.dataset.open.split(":");openWorkspace(term,program,c,type,mode)});
  host.appendChild(x);
 });
}

function showDashboard(){
 $("workspaceView").hidden=true;$("programView").hidden=true;$("dashboardView").hidden=false;
 $("backDashboard").hidden=true;$("backProgram").hidden=true;
}

async function openWorkspace(t,p,c,type,mode){
 S.term=t;S.program=p;S.cls=c;S.type=type;S.mode=t.status==="Warning"?"view":mode;
 $("dashboardView").hidden=true;$("programView").hidden=true;$("workspaceView").hidden=false;
 $("backDashboard").hidden=false;$("backProgram").hidden=false;
 $("ctxTerm").textContent=t.name;$("ctxStatus").textContent=t.status;$("ctxProgram").textContent=p.programCode;$("ctxClass").textContent=c.className;
 $("editBtn").hidden=t.status==="Warning";
 await Promise.all([loadRooms(),loadAssignments(),loadWeeks()]);
 setMode(S.mode);await loadGrid();
}

async function switchType(type){S.type=type;$("schoolBtn").classList.toggle("active",type==="School");$("tuitionBtn").classList.toggle("active",type==="Tuition");await loadGrid();}
function setMode(mode){if(S.term?.status==="Warning")mode="view";S.mode=mode;$("viewBtn").classList.toggle("active",mode==="view");$("editBtn").classList.toggle("active",mode==="edit");$("assignmentPanel").hidden=mode!=="edit";renderSkeleton();renderEntries();}

async function loadWeeks(){
 const r=await fetch(`/Timetable/Weeks?termId=${encodeURIComponent(S.term.termId)}`);const weeks=r.ok?await r.json():[];
 const sel=$("weekDate");sel.innerHTML='<option value="">Weekly template</option>'+weeks.map(w=>`<option value="${w.startDate.slice(0,10)}">Week ${w.weekNumber} · ${dateShort(w.startDate)} – ${dateShort(w.endDate)} · ${esc(w.weekType)}</option>`).join("");
 sel.onchange=loadGrid;
}
async function loadRooms(){const r=await fetch("/Timetable/Rooms");S.rooms=r.ok?await r.json():[];}
async function loadAssignments(){const r=await fetch(`/Timetable/Assignments?classId=${encodeURIComponent(S.cls.classId)}`);S.assignments=r.ok?await r.json():[];renderAssignments();}
function renderAssignments(){
 const q=$("assignmentSearch").value.toLowerCase(),host=$("assignmentList");host.innerHTML="";
 const rows=S.assignments.filter(a=>(a.teacherName+" "+a.courseName).toLowerCase().includes(q));
 if(!rows.length){host.innerHTML="<li style='background:#64748b;cursor:default'>No registered teacher/subject mapping.</li>";return;}
 rows.forEach(a=>{const li=document.createElement("li");li.textContent=`${a.courseName} · ${a.teacherName}`;li.draggable=true;li.ondragstart=e=>e.dataTransfer.setData("assignment",JSON.stringify(a));host.appendChild(li)});
}

async function loadGrid(){
 renderSkeleton();const date=$("weekDate").value;
 const url=`/Timetable/Grid?termId=${encodeURIComponent(S.term.termId)}&classId=${encodeURIComponent(S.cls.classId)}&classType=${S.type}${date?`&weekDate=${date}`:""}`;
 const r=await fetch(url);S.entries=r.ok?await r.json():[];
 await updateWeekNotice();renderEntries();
}

async function updateWeekNotice(){
 const date=$("weekDate").value,box=$("weekNotice");
 if(!date){box.hidden=true;box.textContent="";return;}
 const r=await fetch(`/Timetable/Calendar?termId=${encodeURIComponent(S.term.termId)}`);S.holidays=r.ok?await r.json():[];
 const start=new Date(date+"T00:00:00"),end=new Date(start);end.setDate(end.getDate()+6);
 const inWeek=S.holidays.filter(h=>{const d=new Date(h.eventDate);return d>=start&&d<=end});
 if(!inWeek.length){box.hidden=true;box.textContent="";return;}
 box.hidden=false;box.innerHTML=`<strong>Holiday:</strong> ${inWeek.map(h=>`${dateShort(h.eventDate)} · ${esc(h.name)}`).join(" &nbsp; | &nbsp; ")}`;
}

function renderSkeleton(){
 const hr=$("gridHeaderRow");hr.innerHTML="<th>Day / Time</th>";HOURS.forEach(h=>hr.insertAdjacentHTML("beforeend",`<th>${fmt(h)} - ${fmt(h+1)}</th>`));
 const body=$("gridBody");body.innerHTML="";
 DAYS[S.type].forEach(day=>{
  const tr=document.createElement("tr");tr.dataset.day=day;tr.innerHTML=`<td class="tt-day-label">${DAY_NAME[day]}</td>`;
  HOURS.forEach(h=>{const td=document.createElement("td");td.className="tt-cell";td.dataset.hour=h;td.dataset.day=day;
   if(S.mode==="edit"){td.ondragover=e=>{e.preventDefault();td.classList.add("drag-over")};td.ondragleave=()=>td.classList.remove("drag-over");td.ondrop=e=>{e.preventDefault();td.classList.remove("drag-over");const raw=e.dataTransfer.getData("assignment");if(raw)openCreate(day,h,JSON.parse(raw))};}
   tr.appendChild(td);
  });body.appendChild(tr);
 });
}

function merge(entries){
 const out=[];
 for(const e of [...entries].sort((a,b)=>a.dayOfWeek-b.dayOfWeek||a.startTime.localeCompare(b.startTime))){
  const p=out.at(-1);
  if(p&&p.dayOfWeek===e.dayOfWeek&&p.endTime===e.startTime&&p.courseId===e.courseId&&p.teacherId===e.teacherId&&p.classId===e.classId&&(p.classroomId||"")===(e.classroomId||"")&&p.classType===e.classType&&p.status===e.status&&p.statusReason===e.statusReason){p.endTime=e.endTime;p.ids.push(e.timetableId)}
  else out.push({...e,ids:[e.timetableId]});
 }
 return out;
}

function renderEntries(){
 document.querySelectorAll(".tt-cell").forEach(x=>{x.innerHTML="";x.style.display="";x.removeAttribute("colspan")});
 const rows=S.mode==="view"?merge(S.entries):S.entries;
 rows.forEach(e=>{
  const sh=+e.startTime.split(":")[0],eh=+e.endTime.split(":")[0],tr=document.querySelector(`tr[data-day='${e.dayOfWeek}']`),cell=tr?.querySelector(`[data-hour='${sh}']`);if(!cell)return;
  const span=Math.max(1,eh-sh);if(S.mode==="view")cell.colSpan=span;
  const block=document.createElement("div");block.className=`tt-lesson-block ${(e.status||"Normal").toLowerCase()}`;
  block.innerHTML=`<strong>${esc(e.courseName)}</strong><span>${esc(e.teacherName)}${e.roomName?" · "+esc(e.roomName):""}</span>${e.statusReason?`<small>⚠ ${esc(e.statusReason)}</small>`:""}`;
  if(S.mode==="edit")block.onclick=()=>openEdit(e);cell.appendChild(block);
  if(S.mode==="view")for(let h=sh+1;h<eh;h++){const x=tr.querySelector(`[data-hour='${h}']`);if(x)x.style.display="none";}
 });
}

function openCreate(day,h,a){
 $("lessonModalTitle").textContent="Assign hourly lesson";$("fTimetableId").value="";$("fTeacherId").value=a.teacherId;$("fCourseId").value=a.courseId;$("fDayOfWeek").value=day;
 $("fAssignment").value=`${a.courseName} · ${a.teacherName}`;$("fStartTime").value=`${String(h).padStart(2,"0")}:00`;$("fEndTime").value=`${String(h+1).padStart(2,"0")}:00`;fillRooms();
 $("deleteLessonBtn").hidden=true;configureStatus(day,"Normal",null);showLessonModal();
}
function openEdit(e){
 $("lessonModalTitle").textContent="Edit hourly lesson";$("fTimetableId").value=e.timetableId;$("fTeacherId").value=e.teacherId;$("fCourseId").value=e.courseId;$("fDayOfWeek").value=e.dayOfWeek;
 $("fAssignment").value=`${e.courseName} · ${e.teacherName}`;$("fStartTime").value=e.startTime;$("fEndTime").value=e.endTime;fillRooms(e.classroomId);$("deleteLessonBtn").hidden=false;
 configureStatus(e.dayOfWeek,e.status,e.statusReason);showLessonModal();
}
function configureStatus(day,status,reason){
 const selected=$("weekDate").value,sel=$("fStatus"),label=$("statusDateLabel"),hint=$("statusHint");
 if(!selected){sel.value="Normal";sel.disabled=true;label.textContent="";hint.textContent="Select an actual week to set Normal/Cancelled for a specific lesson date. Holiday and conflict are system status.";return;}
 const d=occurrenceDate(selected,+day);label.textContent=`· ${dateShort(d)}`;sel.disabled=false;sel.value=status==="Cancelled"?"Cancelled":"Normal";
 hint.textContent=reason?`System status: ${reason}. Selecting Normal keeps the lesson; selecting Cancelled cancels this occurrence.`:"Normal/Cancelled applies only to this selected week's lesson. Holiday and conflict are detected automatically.";
}
function fillRooms(v=""){$("fClassroomId").innerHTML='<option value="">-- none --</option>'+S.rooms.map(r=>`<option value="${r.id}" ${r.id===v?"selected":""}>${esc(r.name)}</option>`).join("");}
function showLessonModal(){$("lessonModal").hidden=false;$("lessonModalError").hidden=true;}
function closeLessonModal(){$("lessonModal").hidden=true;}

async function saveLesson(e){
 e.preventDefault();
 const p={timetableId:$("fTimetableId").value||null,termId:S.term.termId,teacherId:$("fTeacherId").value,courseId:$("fCourseId").value,classId:S.cls.classId,classroomId:$("fClassroomId").value||null,dayOfWeek:+$("fDayOfWeek").value,startTime:$("fStartTime").value,endTime:$("fEndTime").value,classType:S.type==="School"?0:1};
 let r=await fetch("/Timetable/Save",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(p)});
 if(!r.ok){showLessonError(await r.text());return;}
 const saved=await r.json();
 const selected=$("weekDate").value;
 if(selected&&!$("fStatus").disabled){
  const statusPayload={timetableId:saved.timetableId,date:occurrenceDate(selected,p.dayOfWeek),status:$("fStatus").value,note:null};
  r=await fetch("/Timetable/SetOccurrenceStatus",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(statusPayload)});
  if(!r.ok){showLessonError(await r.text());return;}
 }
 closeLessonModal();await loadGrid();
}
function showLessonError(message){$("lessonModalError").textContent=message;$("lessonModalError").hidden=false;}
async function deleteLesson(){const id=$("fTimetableId").value;if(!id||!confirm("Delete this hourly lesson from the weekly template?"))return;const r=await fetch(`/Timetable/Delete?id=${encodeURIComponent(id)}`,{method:"POST"});if(!r.ok){showLessonError(await r.text());return;}closeLessonModal();await loadGrid();}


function openSemesterModal(term=null){
 resetForceActiveConfirmation();
 $("semesterError").hidden=true;
 $("semesterTermId").value=term?.termId||"";
 $("semesterModalTitle").textContent=term?"Edit Incoming Semester Dates":"Create New Semester";
 $("semesterSubmitBtn").textContent=term?"Save Dates":"Create";
 $("semesterName").disabled=!!term;
 $("semesterName").value=term?.name||"";
 $("semesterStart").value=term?term.startDate.slice(0,10):"";
 $("semesterEnd").value=term?term.endDate.slice(0,10):"";
 prepareSemesterForm();
 $("semesterModal").hidden=false;
}

function prepareSemesterForm(){
 const today=localIso(new Date());
 const start=$("semesterStart"),end=$("semesterEnd");
 start.min=today;end.min=today;
 const syncEndLimits=()=>{
  if(!start.value)return;
  end.min=start.value;
  const max=new Date(start.value+"T00:00:00");max.setFullYear(max.getFullYear()+1);
  end.max=localIso(max);
  if(end.value&&(end.value<end.min||end.value>end.max))end.value="";
 };
 start.onchange=syncEndLimits;syncEndLimits();
}
function semesterClientError(){
 const name=$("semesterName").value.trim(),start=$("semesterStart").value,end=$("semesterEnd").value,today=localIso(new Date());
 if(name.length<3)return "Semester name must contain at least 3 characters.";
 if(!start||!end)return "Semester name, start date and end date are required.";
 if(start<today)return "Start date cannot be in the past.";
 if(end<=start)return "End date must be later than the start date.";
 const max=new Date(start+"T00:00:00");max.setFullYear(max.getFullYear()+1);
 if(end>localIso(max))return "Semester duration cannot exceed one year.";
 return "";
}
async function createSemester(e){
 e.preventDefault();
 const error=semesterClientError();
 if(error){showSemesterError(error);return;}
 const termId=$("semesterTermId").value;
 if(termId){
  const payload={termId,startDate:$("semesterStart").value,endDate:$("semesterEnd").value};
  const r=await fetch("/Timetable/UpdateTermDates",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(payload)});
  if(!r.ok){const data=await readResponseMessage(r);showSemesterError(data.message);return;}
  closeSemesterModal();await dashboard();return;
 }
 await submitNewSemester(false);
}
async function submitNewSemester(forceActivate){
 const payload={name:$("semesterName").value.trim(),startDate:$("semesterStart").value,endDate:$("semesterEnd").value,forceActivate};
 const r=await fetch("/Timetable/CreateTerm",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(payload)});
 if(r.status===409){
  const data=await readResponseMessage(r);
  if(data.code==="FORCE_ACTIVE_CONFIRMATION_REQUIRED"){
   showForceActiveConfirmation(data.message);
   return;
  }
  showSemesterError(data.message||"Unable to create semester.");
  return;
 }
 if(!r.ok){
  const data=await readResponseMessage(r);
  showSemesterError(data.message||"Unable to create semester.");
  return;
 }
 closeSemesterModal();await dashboard();
}
async function readResponseMessage(r){
 const contentType=r.headers.get("content-type")||"";
 if(contentType.includes("application/json")){
  try{
   const data=await r.json();
   return {code:data?.code||"",message:data?.message||data?.title||"Request failed."};
  }catch{}
 }
 try{
  const text=(await r.text())||"";
  if(r.status>=500) return {code:"",message:"A server error occurred while processing the semester. Please try again or check the server log."};
  return {code:"",message:text||"Request failed."};
 }catch{return {code:"",message:"Request failed."};}
}
function showForceActiveConfirmation(message){
 $("semesterError").hidden=true;
 $("forceActiveMessage").textContent=message||"This semester starts today. Creating it will make the semester Active immediately.";
 $("forceActiveConfirm").hidden=false;
 $("semesterSubmitBtn").hidden=true;
 $("cancelSemesterBtn").hidden=true;
 ["semesterName","semesterStart","semesterEnd"].forEach(id=>$(id).disabled=true);
}
function cancelForceActiveConfirmation(){
 resetForceActiveConfirmation();
}
async function confirmForceActiveCreation(){
 $("confirmForceActiveBtn").disabled=true;
 try{await submitNewSemester(true);}finally{$("confirmForceActiveBtn").disabled=false;}
}
function resetForceActiveConfirmation(){
 const box=$("forceActiveConfirm");
 if(box)box.hidden=true;
 const submit=$("semesterSubmitBtn"),cancel=$("cancelSemesterBtn");
 if(submit)submit.hidden=false;if(cancel)cancel.hidden=false;
 ["semesterName","semesterStart","semesterEnd"].forEach(id=>{const el=$(id);if(el)el.disabled=false;});
 if($("semesterTermId")?.value)$("semesterName").disabled=true;
}
function showSemesterError(message){$("semesterError").textContent=message;$("semesterError").hidden=false;}
function closeSemesterModal(){resetForceActiveConfirmation();$("semesterModal").hidden=true;$("semesterForm").reset();$("semesterName").disabled=false;$("semesterTermId").value="";}
async function handleTermAction(action,term){
 if(action==="edit"){openSemesterModal(term);return;}
 if(action==="archive"){
  if(!confirm(`Move ${term.name} to History? It will become Warning and will no longer be published to teachers/students.`))return;
  const r=await fetch(`/Timetable/CompleteTerm?termId=${encodeURIComponent(term.termId)}`,{method:"POST"});
  if(!r.ok){alert(await r.text());return;}await dashboard();return;
 }
 if(action==="restore"){
  const r=await fetch(`/Timetable/RestoreTerm?termId=${encodeURIComponent(term.termId)}`,{method:"POST"});
  if(!r.ok){alert(await r.text());return;}
  S.bucket="current";
  document.querySelectorAll("[data-status]").forEach(x=>x.classList.toggle("active",x.dataset.status==="current"));
  await dashboard();
  return;
 }
 if(action==="activate-restored"){
  if(!confirm(`Set ${term.name} as the Active semester? Only Active timetables are published to teachers and students.`))return;
  const r=await fetch(`/Timetable/ActivateRestoredTerm?termId=${encodeURIComponent(term.termId)}`,{method:"POST"});
  if(!r.ok){alert(await r.text());return;}
  await dashboard();
 }
}

async function prepareHolidayHistory(){
 if(!S.dashboard)return;
 const terms=[...(S.dashboard.current||[]),...(S.dashboard.completed||[])];
 const sel=$("holidayTerm");
 const previous=sel.value;
 sel.innerHTML=terms.map(t=>`<option value="${t.termId}" data-status="${esc(t.status)}">${esc(t.name)} · ${esc(t.status)}</option>`).join("");
 if(previous&&terms.some(t=>t.termId===previous))sel.value=previous;
 else if(terms.length)sel.value=terms[0].termId;
 updateHolidayEditingState();
 await loadHolidayList();
}
function selectedHolidayTerm(){
 const terms=[...(S.dashboard?.current||[]),...(S.dashboard?.completed||[])];
 return terms.find(t=>t.termId===$("holidayTerm").value);
}
function updateHolidayEditingState(){
 const term=selectedHolidayTerm(),readOnly=!term||term.status==="Warning";
 $("holidayForm").querySelectorAll("input,select,button").forEach(x=>x.disabled=readOnly);
 $("loadMalaysiaHolidaysBtn").disabled=readOnly;
 $("chooseHolidayFile").disabled=readOnly;
 $("holidayDropZone").classList.toggle("disabled",readOnly);
}
async function loadHolidayList(){
 updateHolidayEditingState();
 const term=selectedHolidayTerm(),termId=$("holidayTerm").value,host=$("holidayList");
 if(!termId){host.innerHTML="<div class='tt-empty'>No semester available.</div>";return;}
 const r=await fetch(`/Timetable/Calendar?termId=${encodeURIComponent(termId)}`);
 const rows=r.ok?await r.json():[];
 host.innerHTML=rows.length?"":"<div class='tt-empty'>No holidays saved for this semester.</div>";
 rows.forEach(h=>{
  const row=document.createElement("div");row.className="tt-holiday-row";
  const canDelete=term&&term.status!=="Warning";
  row.innerHTML=`<strong>${dateShort(h.eventDate)}</strong><span>${esc(h.name)}</span><span><small>${esc(h.eventType)}</small></span><span>${esc(term?.name||"")}</span>${canDelete?'<button type="button">Delete</button>':'<span class="tt-readonly">History</span>'}`;
  if(canDelete)row.querySelector("button").onclick=()=>deleteHoliday(h.calendarEventId);
  host.appendChild(row);
 });
}
async function addHoliday(e){
 e.preventDefault();
 const payload={termId:$("holidayTerm").value,eventDate:$("holidayDate").value,name:$("holidayName").value.trim(),eventType:$("holidayType").value,affectsClasses:true};
 const r=await fetch("/Timetable/AddCalendarEvent",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(payload)});
 if(!r.ok){showHolidayError(await r.text());return;}
 $("holidayError").hidden=true;$("holidayName").value="";$("holidayDate").value="";await loadHolidayList();if(S.term?.termId===payload.termId)await loadGrid();
}
async function loadMalaysiaHolidays(){
 const termId=$("holidayTerm").value;if(!termId)return;
 const r=await fetch(`/Timetable/LoadMalaysia2026Holidays?termId=${encodeURIComponent(termId)}`,{method:"POST"});
 if(!r.ok){showHolidayError(await r.text());return;}
 const result=await r.json();$("holidayImportResult").textContent=`${result.added} Malaysia holiday(s) added.`;$("holidayImportResult").hidden=false;await loadHolidayList();
}
async function importHolidayFile(file){
 const term=selectedHolidayTerm();if(!term||term.status==="Warning")return;
 if(!/\.(txt|csv)$/i.test(file.name)){showHolidayError("Only .txt or .csv files are accepted.");return;}
 const fd=new FormData();fd.append("termId",term.termId);fd.append("file",file);
 const r=await fetch("/Timetable/ImportCalendarEvents",{method:"POST",body:fd});
 if(!r.ok){showHolidayError(await r.text());return;}
 const result=await r.json();
 const extra=result.errors?.length?` Errors: ${result.errors.join(" | ")}`:"";
 $("holidayImportResult").textContent=`Imported ${result.added}; skipped ${result.skipped}.${extra}`;$("holidayImportResult").hidden=false;$("holidayError").hidden=true;$("holidayFile").value="";await loadHolidayList();
}
function showHolidayError(message){$("holidayError").textContent=message;$("holidayError").hidden=false;}
async function deleteHoliday(id){if(!confirm("Delete this holiday?"))return;const r=await fetch(`/Timetable/DeleteCalendarEvent?id=${encodeURIComponent(id)}`,{method:"POST"});if(!r.ok){showHolidayError(await r.text());return;}await loadHolidayList();if(S.term?.termId===$("holidayTerm").value)await loadGrid();}

function occurrenceDate(weekStart,day){const d=new Date(weekStart+"T00:00:00"),delta=(day-d.getDay()+7)%7;d.setDate(d.getDate()+delta);return localIso(d);}
function localIso(d){const y=d.getFullYear(),m=String(d.getMonth()+1).padStart(2,"0"),day=String(d.getDate()).padStart(2,"0");return `${y}-${m}-${day}`;}
function dateText(v){return new Date(v).toLocaleDateString();}
function dateShort(v){return new Date(v).toLocaleDateString(undefined,{day:"2-digit",month:"short",year:"numeric"});}
function fmt(h){const p=h>=12?"PM":"AM",d=h>12?h-12:h;return `${d}:00 ${p}`;}
})();
