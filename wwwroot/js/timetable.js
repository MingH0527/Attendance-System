(()=>{"use strict";
const HOURS=[8,9,10,11,12,13,14,15,16,17];
const DAYS={School:[1,2,3,4,5],Tuition:[6,0]};
const DAY_NAME={0:"Sunday",1:"Monday",2:"Tuesday",3:"Wednesday",4:"Thursday",5:"Friday",6:"Saturday"};
const S={bucket:"current",dashboard:null,term:null,program:null,cls:null,type:"School",mode:"view",entries:[],assignments:[],rooms:[],holidays:[],replacementRequired:[]};
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
 $("fStatus").onchange=()=>{$("lessonModalError").hidden=true;};
 $("fStartTime").onchange=$("fEndTime").onchange=refreshOccurrenceRooms;
 $("deleteLessonBtn").onclick=deleteLesson;
 $("assignmentSearch").oninput=renderAssignments;
 $("newSemesterBtn").onclick=()=>openSemesterModal();
 $("closeSemesterBtn").onclick=$("cancelSemesterBtn").onclick=closeSemesterModal;
 $("semesterForm").onsubmit=createSemester;
 $("cancelForceActiveBtn").onclick=cancelForceActiveConfirmation;
 $("confirmForceActiveBtn").onclick=confirmForceActiveCreation;
 $("holidayForm").onsubmit=addHoliday;
 $("auditFilter").onchange=loadAudit;
 $("closeReplacementBtn").onclick=closeReplacementModal;
 $("replacementForm").onsubmit=saveReplacement;
 $("rDate").onchange=$("rStart").onchange=$("rEnd").onchange=refreshReplacementRooms;
 $("loadMalaysiaHolidaysBtn").onclick=loadMalaysiaHolidays;
 $("chooseHolidayFile").onclick=()=>$("holidayFile").click();
 $("holidayFile").onchange=()=>{const f=$("holidayFile").files?.[0];if(f)importHolidayFile(f)};
 const dz=$("holidayDropZone");
 dz.onclick=e=>{if(e.target.tagName!=="BUTTON")$("holidayFile").click()};
 dz.ondragenter=e=>{e.preventDefault();dz.classList.add("drag-over")};
 dz.ondragover=e=>{e.preventDefault();if(e.dataTransfer)e.dataTransfer.dropEffect="copy";dz.classList.add("drag-over")};
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
 const holidayMode=S.bucket==="holiday",auditMode=S.bucket==="audit";
 $("holidayHistoryPanel").hidden=!holidayMode;
 $("auditPanel").hidden=!auditMode;
 host.hidden=holidayMode||auditMode;
 if(holidayMode){await prepareHolidayHistory();return;}
 if(auditMode){await loadAudit();return;}
 const terms=S.bucket==="current"?S.dashboard.current:S.dashboard.completed;
 $("newSemesterBtn").disabled=(S.dashboard.current||[]).length>=2;
 $("newSemesterBtn").title=$("newSemesterBtn").disabled?"Maximum two semesters can be kept on the timetable page.":"Create a new semester";
 if(!terms.length){host.innerHTML="<div class='tt-empty'>No academic terms in this section.</div>";return;}
 terms.forEach(t=>{
  const card=document.createElement("article");
  card.className=`tt-term-card ${t.status==="Warning"?"tt-term-warning":""} ${t.status==="Abandoned"?"tt-term-abandoned":""}`;
  const restoredStatusChoice=t.isRestored
   ? `<button type="button" data-term-action="activate-restored" class="tt-btn-secondary tt-set-active-btn">Set Active</button>`
   : "";
  const actions=S.bucket==="current"
   ? `<div class="tt-term-actions">${t.status==="Incoming"?'<button type="button" data-term-action="edit" class="tt-btn-secondary">Edit Dates</button>':''}${restoredStatusChoice}<button type="button" data-term-action="archive" class="tt-btn-secondary">Move to History</button></div>`
   : t.status==="Abandoned"
     ? `<div class="tt-term-actions"><button type="button" data-term-action="undo-abandon" class="tt-btn-recover">Undo Abandon</button></div>`
     : `<div class="tt-term-actions"><button type="button" data-term-action="restore" class="tt-btn-secondary">Restore</button><button type="button" data-term-action="abandon" class="tt-btn-secondary">Abandon</button></div>`;
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
 S.term=t;S.program=p;S.cls=c;S.type=type;S.mode=(t.status==="Warning"||t.status==="Abandoned")?"view":mode;
 $("dashboardView").hidden=true;$("programView").hidden=true;$("workspaceView").hidden=false;
 $("backDashboard").hidden=false;$("backProgram").hidden=false;
 $("ctxTerm").textContent=t.name;$("ctxStatus").textContent=t.status;$("ctxProgram").textContent=p.programCode;$("ctxClass").textContent=c.className;
 $("editBtn").hidden=t.status==="Warning"||t.status==="Abandoned";
 await Promise.all([loadRooms(),loadAssignments(),loadWeeks()]);
 setMode(S.mode);await loadGrid();
}

async function switchType(type){S.type=type;$("schoolBtn").classList.toggle("active",type==="School");$("tuitionBtn").classList.toggle("active",type==="Tuition");await loadGrid();}
function setMode(mode){if(S.term?.status==="Warning"||S.term?.status==="Abandoned")mode="view";S.mode=mode;$("viewBtn").classList.toggle("active",mode==="view");$("editBtn").classList.toggle("active",mode==="edit");$("assignmentPanel").hidden=mode!=="edit";renderSkeleton();renderEntries();updateReplacementPanel();}

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
 const r=await fetch(url);
 if(!r.ok){
  S.entries=[];
  const raw=await r.text();
  const box=$("weekNotice");
  box.hidden=false;
  box.textContent=raw||"Unable to load this Week. Please make sure the latest database migration has been applied.";
  box.classList.add("tt-room-alert");
  renderEntries();
  return;
 }
 S.entries=await r.json();
 $("weekNotice").classList.remove("tt-room-alert");
 await updateWeekNotice();renderEntries();await updateReplacementPanel();
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
  const selectedWeek=$("weekDate").value;
  const occurrence=selectedWeek?occurrenceDate(selectedWeek,+day):null;
  const outside=occurrence&&!isDateInsideSemester(occurrence);
  const tr=document.createElement("tr");tr.dataset.day=day;
  tr.innerHTML=`<td class="tt-day-label ${outside?"tt-day-outside":""}"><strong>${DAY_NAME[day]}</strong>${occurrence?`<span>${dateDayShort(occurrence)}</span>`:""}${outside?'<small>Outside Semester</small>':""}</td>`;
  HOURS.forEach(h=>{const td=document.createElement("td");td.className="tt-cell";td.dataset.hour=h;td.dataset.day=day;
   if(outside){
    td.classList.add("tt-outside-semester");
    td.dataset.outsideSemester="true";
    td.title=`${dateShort(occurrence)} is outside ${S.term.name}.`;
   }else if(S.mode==="edit"&&!selectedWeek){
    td.ondragover=e=>{e.preventDefault();td.classList.add("drag-over")};
    td.ondragleave=()=>td.classList.remove("drag-over");
    td.ondrop=e=>{e.preventDefault();td.classList.remove("drag-over");const raw=e.dataTransfer.getData("assignment");if(raw)openCreate(day,h,JSON.parse(raw))};
   }
   tr.appendChild(td);
  });body.appendChild(tr);
 });
}

function merge(entries){
 const out=[];
 for(const e of [...entries].sort((a,b)=>a.dayOfWeek-b.dayOfWeek||a.startTime.localeCompare(b.startTime))){
  const p=out.at(-1);
  if(p&&p.dayOfWeek===e.dayOfWeek&&p.endTime===e.startTime&&p.courseId===e.courseId&&p.teacherId===e.teacherId&&p.classId===e.classId&&(p.classroomId||"")===(e.classroomId||"")&&p.classType===e.classType&&p.status===e.status&&p.statusReason===e.statusReason&&p.isReplacement===e.isReplacement&&p.replacementId===e.replacementId){p.endTime=e.endTime;p.ids.push(e.timetableId)}
  else out.push({...e,ids:[e.timetableId]});
 }
 return out;
}

function renderEntries(){
 document.querySelectorAll(".tt-cell").forEach(x=>{x.innerHTML="";x.style.display="";x.removeAttribute("colspan");x.classList.remove("has-overlap")});
 const visible=S.mode==="view"?S.entries.filter(e=>!(e.status==="Cancelled"&&e.hasReplacement)):S.entries;
 const rows=S.mode==="view"?merge(visible):visible;
 rows.forEach(e=>{
  const sh=+e.startTime.split(":")[0],eh=+e.endTime.split(":")[0],tr=document.querySelector(`tr[data-day='${e.dayOfWeek}']`),cell=tr?.querySelector(`[data-hour='${sh}']`);if(!cell)return;
  if(cell.dataset.outsideSemester==="true")return;
  const span=Math.max(1,eh-sh);if(S.mode==="view")cell.colSpan=span;
  const block=document.createElement("div");block.className=`tt-lesson-block ${(e.status||"Normal").toLowerCase()}`;
  block.innerHTML=`<strong>${esc(e.courseName)}${e.isReplacement?'<em class="tt-replacement-badge">Replacement</em>':e.isUpdated?'<em class="tt-updated-badge">Updated</em>':''}</strong><span>${esc(e.teacherName)}${e.roomName?" · "+esc(e.roomName):""}</span>${e.statusReason?`<small>${e.isReplacement?"↪":"⚠"} ${esc(e.statusReason)}</small>`:""}`;
  if(S.mode==="edit"&&!e.isReplacement)block.onclick=()=>openEdit(e);cell.appendChild(block);
  if(S.mode==="view")for(let h=sh+1;h<eh;h++){const x=tr.querySelector(`[data-hour='${h}']`);if(x)x.style.display="none";}
 });
 document.querySelectorAll(".tt-cell").forEach(cell=>{if(cell.querySelectorAll(":scope > .tt-lesson-block").length>1)cell.classList.add("has-overlap")});
}

async function openCreate(day,h,a){
 $("lessonModalTitle").textContent="Assign hourly lesson";$("fTimetableId").value="";$("fTeacherId").value=a.teacherId;$("fCourseId").value=a.courseId;$("fDayOfWeek").value=day;
 $("fAssignment").value=`${a.courseName} · ${a.teacherName}`;$("fStartTime").value=`${String(h).padStart(2,"0")}:00`;$("fEndTime").value=`${String(h+1).padStart(2,"0")}:00`;
 await fillRooms("",day,$("fStartTime").value,$("fEndTime").value,null);
 $("deleteLessonBtn").hidden=true;configureStatus(day,"Normal",null);showLessonModal();
}
async function openEdit(e){
 if($("weekDate").value){
  const date=occurrenceDate($("weekDate").value,+e.dayOfWeek);
  if(!isDateInsideSemester(date))return;
 }
 $("lessonModalTitle").textContent=$("weekDate").value?"Edit this lesson date":"Edit weekly template lesson";$("fTimetableId").value=e.timetableId;$("fTeacherId").value=e.teacherId;$("fCourseId").value=e.courseId;$("fDayOfWeek").value=e.dayOfWeek;
 $("fAssignment").value=`${e.courseName} · ${e.teacherName}`;$("fStartTime").value=e.startTime;$("fEndTime").value=e.endTime;
 await fillRooms(e.classroomId,e.dayOfWeek,e.startTime,e.endTime,e.timetableId);$("deleteLessonBtn").hidden=!!$("weekDate").value;
 const actual=!!$("weekDate").value; $("fStartTime").readOnly=!actual; $("fEndTime").readOnly=!actual;
 configureStatus(e.dayOfWeek,e.status,e.statusReason,e.hasReplacement);showLessonModal();
}
function configureStatus(day,status,reason,hasReplacement=false){
 const selected=$("weekDate").value,sel=$("fStatus"),label=$("statusDateLabel"),hint=$("statusHint"),saveBtn=$("lessonForm").querySelector('button[type="submit"]');
 sel.disabled=false;$("fClassroomId").disabled=false;saveBtn.disabled=false;
 [...sel.options].forEach(o=>o.disabled=false);
 if(!selected){sel.value="Normal";label.textContent="";hint.textContent="Weekly template is the parent schedule. To change lesson status, select the specific Week first.";return;}
 const d=occurrenceDate(selected,+day);label.textContent=`· ${dateShort(d)}`;sel.value=status==="Cancelled"?"Cancelled":"Normal";
 const lessonStart=new Date(`${d}T${$("fStartTime").value}:00`);
 if(lessonStart<=new Date()){
  sel.disabled=true;$("fClassroomId").disabled=true;$("fStartTime").readOnly=true;$("fEndTime").readOnly=true;saveBtn.disabled=true;
  hint.textContent=`This lesson started or passed on ${dateShort(d)} and is now read-only. Its classroom, time and status can no longer be changed.`;
  return;
 }
 if(status==="Cancelled"&&hasReplacement){
  const normalOption=[...sel.options].find(o=>o.value==="Normal");if(normalOption)normalOption.disabled=true;
  sel.value="Cancelled";
  hint.textContent=`A replacement class has already been scheduled for ${dateShort(d)}. This original lesson must remain Cancelled and cannot be changed back to Normal unless the replacement is cancelled or removed first.`;
  return;
 }
 hint.textContent=reason?`System status: ${reason}. Any change here applies only to ${dateShort(d)}; the weekly template and other weeks stay unchanged.`:`Changes here apply only to ${dateShort(d)}; the weekly template and other weeks stay unchanged.`;
}
async function refreshOccurrenceRooms(){
 if(!$("weekDate").value||!$("fTimetableId").value)return;
 const s=$("fStartTime").value,e=$("fEndTime").value;if(!s||!e||s>=e)return;
 await fillRooms($("fClassroomId").value,+$("fDayOfWeek").value,s,e,$("fTimetableId").value);
}
async function fillRooms(v="",day,start,end,timetableId=null){
 const selected=$("weekDate").value;
 let url;
 if(selected){
  const date=occurrenceDate(selected,+day);
  url=`/Timetable/RoomAvailability?termId=${encodeURIComponent(S.term.termId)}&date=${encodeURIComponent(date)}&startTime=${encodeURIComponent(start)}&endTime=${encodeURIComponent(end)}${timetableId?`&excludeTimetableId=${encodeURIComponent(timetableId)}`:""}`;
 }else{
  url=`/Timetable/TemplateRoomAvailability?termId=${encodeURIComponent(S.term.termId)}&dayOfWeek=${encodeURIComponent(day)}&startTime=${encodeURIComponent(start)}&endTime=${encodeURIComponent(end)}${timetableId?`&excludeTimetableId=${encodeURIComponent(timetableId)}`:""}`;
 }
 const r=await fetch(url);const rooms=r.ok?await r.json():S.rooms.map(x=>({...x,available:true,reason:null}));
 const available=rooms.filter(x=>x.available),hint=$("roomHint");
 $("fClassroomId").innerHTML=(available.length?'<option value="">-- select classroom --</option>':'<option value="">-- No classroom available --</option>')+rooms.map(x=>`<option value="${x.id}" ${x.id===v?"selected":""} ${!x.available&&x.id!==v?"disabled":""}>${esc(x.name)}${x.available?"":" · occupied"}</option>`).join("");
 hint.textContent=available.length?`${available.length} of 6 classroom(s) available. Occupied rooms cannot be selected.`:"All 6 fixed classrooms are occupied at this time. The lesson will remain yellow (Attention) until Admin changes the time/day or another room becomes available.";
 hint.classList.toggle("tt-room-alert",!available.length);
}
function showLessonModal(){$("lessonModal").hidden=false;$("lessonModalError").hidden=true;}
function closeLessonModal(){$("lessonModal").hidden=true;}

async function saveLesson(e){
 e.preventDefault();
 const selected=$("weekDate").value;
 if(!selected && $("fTimetableId").value && $("fStatus").value!=="Normal"){
  showLessonError("Please select the Week and specific lesson date you want to change. Status cannot be changed from the Weekly template because it would affect the parent schedule.");
  return;
 }
 if(selected&&$("fTimetableId").value){
  const p={timetableId:$("fTimetableId").value,date:occurrenceDate(selected,+$("fDayOfWeek").value),classroomId:$("fClassroomId").value||null,startTime:$("fStartTime").value,endTime:$("fEndTime").value,status:$("fStatus").value,note:null};
  const r=await fetch("/Timetable/SaveOccurrence",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(p)});
  if(!r.ok){showLessonError(await r.text());return;}
  closeLessonModal();await loadGrid();return;
 }
 if(selected){showLessonError("Adding a new recurring lesson from an actual week is disabled because it would change the whole semester. Use Weekly template to add recurring lessons; use the replacement function for a one-day make-up class.");return;}
 if(!$("fTimetableId").value&&$("fStatus").value!=="Normal"){showLessonError("A new lesson cannot be created as Cancelled. Create the lesson first, then select a specific Week and lesson date to cancel it.");return;}
 const p={timetableId:$("fTimetableId").value||null,termId:S.term.termId,teacherId:$("fTeacherId").value,courseId:$("fCourseId").value,classId:S.cls.classId,classroomId:$("fClassroomId").value||null,dayOfWeek:+$("fDayOfWeek").value,startTime:$("fStartTime").value,endTime:$("fEndTime").value,classType:S.type==="School"?0:1,status:$("fStatus").value};
 const r=await fetch("/Timetable/Save",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(p)});
 if(!r.ok){showLessonError(await r.text());return;}
 closeLessonModal();await loadGrid();
}
function showLessonError(message){$("lessonModalError").textContent=message;$("lessonModalError").hidden=false;}
async function deleteLesson(){const id=$("fTimetableId").value;if(!id||!confirm("Delete this hourly lesson from the weekly template?"))return;const r=await fetch(`/Timetable/Delete?id=${encodeURIComponent(id)}`,{method:"POST"});if(!r.ok){showLessonError(await r.text());return;}closeLessonModal();await loadGrid();}



async function updateReplacementPanel(){
 const panel=$("replacementPanel"),host=$("replacementRequiredList"),week=$("weekDate").value;
 const show=S.type==="Tuition"&&S.mode==="edit"&&!!week&&S.term?.status!=="Warning"&&S.term?.status!=="Abandoned";
 panel.hidden=!show;if(!show){host.innerHTML="";return;}
 const r=await fetch(`/Timetable/ReplacementRequired?termId=${encodeURIComponent(S.term.termId)}&classId=${encodeURIComponent(S.cls.classId)}&weekDate=${encodeURIComponent(week)}`);
 S.replacementRequired=r.ok?await r.json():[];
 if(!S.replacementRequired.length){host.innerHTML='<div class="tt-replacement-empty">No unresolved replacement is waiting for this class.</div>';return;}
 host.innerHTML="";
 S.replacementRequired.forEach(x=>{
  const card=document.createElement("article");card.className="tt-replacement-card pending";
  const scope=x.isCurrentWeek?'<em class="tt-replacement-scope current">This Week</em>':'<em class="tt-replacement-scope previous">Previous Pending</em>';
  const availability=x.canScheduleReplacement
   ? `<span class="tt-replacement-available">Next valid Tuition date: ${dateShort(x.nextValidTuitionDate)}</span>`
   : '<span class="tt-replacement-unavailable">No Saturday/Sunday Tuition date remains before semester end. Admin may confirm that this cancelled lesson will not have a replacement class.</span>';
  const action=x.canScheduleReplacement
   ? '<button type="button" class="tt-btn-primary" data-action="schedule">Schedule Replacement</button>'
   : '<button type="button" class="tt-btn-secondary" data-action="waive">Confirm Cancellation — No Replacement</button>';
  card.innerHTML=`<div>${scope}<strong>${esc(x.courseName)} · ${esc(x.teacherName)}</strong><span>🟥 Cancelled ${dateShort(x.occurrenceDate)} · ${esc(x.startTime)}-${esc(x.endTime)}${x.roomName?" · "+esc(x.roomName):""}</span>${availability}</div>${action}`;
  const button=card.querySelector("button");
  if(x.canScheduleReplacement)button.onclick=()=>openReplacementModal(x);else button.onclick=()=>waiveReplacement(x);
  host.appendChild(card);
 });
}
function openReplacementModal(x){
 $("replacementError").hidden=true;$("rTimetableId").value=x.timetableId;$("rSourceDate").value=x.occurrenceDate.slice(0,10);$("rTeacherId").value=x.teacherId;
 $("rOriginal").value=`${x.courseName} · ${x.teacherName} · ${dateShort(x.occurrenceDate)} · ${x.startTime}-${x.endTime}`;
 $("rDate").min=x.occurrenceDate.slice(0,10);$("rDate").max=S.term.endDate.slice(0,10);
 const next=x.nextValidTuitionDate?String(x.nextValidTuitionDate).slice(0,10):"";
 $("rDate").value=next;$("rStart").value=x.startTime;$("rEnd").value=x.endTime;$("replacementModal").hidden=false;
 if(!next){$("replacementError").textContent="No valid Tuition Saturday/Sunday remains before the semester ends.";$("replacementError").hidden=false;$("rClassroom").innerHTML='<option value="">-- No valid replacement date --</option>';return;}
 refreshReplacementRooms();
}
async function waiveReplacement(x){
 if(!confirm(`No valid Tuition weekend remains for the cancelled lesson on ${dateShort(x.occurrenceDate)}. I confirm this lesson is cancelled and no replacement class will be scheduled. Continue?`))return;
 const r=await fetch("/Timetable/WaiveReplacement",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({sourceTimetableId:x.timetableId,sourceOccurrenceDate:x.occurrenceDate,reason:"No valid Tuition weekend remains before semester end."})});
 if(!r.ok){alert(await r.text());return;}
 await updateReplacementPanel();
}
function closeReplacementModal(){$("replacementModal").hidden=true;}
async function refreshReplacementRooms(){
 if($("replacementModal").hidden)return;const date=$("rDate").value,s=$("rStart").value,e=$("rEnd").value;if(!date||!s||!e||s>=e)return;
 const r=await fetch(`/Timetable/ReplacementRoomAvailability?termId=${encodeURIComponent(S.term.termId)}&date=${encodeURIComponent(date)}&startTime=${encodeURIComponent(s)}&endTime=${encodeURIComponent(e)}`);
 const rooms=r.ok?await r.json():[];const available=rooms.filter(x=>x.available);$("rClassroom").innerHTML=(available.length?'<option value="">-- select classroom --</option>':'<option value="">-- No classroom available --</option>')+rooms.map(x=>`<option value="${x.id}" ${!x.available?"disabled":""}>${esc(x.name)}${x.available?"":" · occupied"}</option>`).join("");
 $("replacementRoomHint").textContent=available.length?`${available.length} of 6 classroom(s) available.`:"All 6 fixed classrooms are occupied. Please choose another replacement time.";
 $("replacementRoomHint").classList.toggle("tt-room-alert",!available.length);
}
async function saveReplacement(e){
 e.preventDefault();$("replacementError").hidden=true;
 const p={sourceTimetableId:$("rTimetableId").value,sourceOccurrenceDate:$("rSourceDate").value,date:$("rDate").value,startTime:$("rStart").value,endTime:$("rEnd").value,classroomId:$("rClassroom").value||null};
 const r=await fetch("/Timetable/CreateReplacement",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(p)});
 if(!r.ok){$("replacementError").textContent=await r.text();$("replacementError").hidden=false;return;}
 const saved=await r.json();

 // Once scheduled, the item leaves Replacement Required and Admin must be taken
 // to the actual Week containing the new Tuition lesson. Use the Week returned by
 // the server instead of assuming every academic Week is exactly seven days.
 const weekSelect=$("weekDate");
 let targetValue=saved.replacementWeekStart ? String(saved.replacementWeekStart).slice(0,10) : "";

 // If Weeks were regenerated or the current select is stale, reload it once.
 if(targetValue && ![...weekSelect.options].some(o=>o.value===targetValue)){
  await loadWeeks();
 }

 if(targetValue && [...weekSelect.options].some(o=>o.value===targetValue)){
  weekSelect.value=targetValue;
 }else{
  // Safe fallback for legacy data without an AcademicWeek row.
  const replacementDate=new Date((saved.replacementDate||p.date).slice(0,10)+"T00:00:00");
  const target=[...weekSelect.options].find(o=>{
   if(!o.value)return false;
   const start=new Date(o.value+"T00:00:00");
   const next=[...weekSelect.options].find(x=>x.value&&new Date(x.value+"T00:00:00")>start);
   const end=next?new Date(new Date(next.value+"T00:00:00").getTime()-86400000):new Date(S.term.endDate);
   return replacementDate>=start&&replacementDate<=end;
  });
  if(target)weekSelect.value=target.value;
 }

 // Replacement classes always live in Tuition. Reload after changing the Week so
 // the new class is immediately rendered in Saturday/Sunday instead of vanishing.
 S.type="Tuition";
 $("schoolBtn").classList.remove("active");
 $("tuitionBtn").classList.add("active");
 closeReplacementModal();
 await loadGrid();

 // If no Week could be resolved, do not silently leave Admin with an empty grid.
 if(!S.entries.some(x=>x.isReplacement&&x.replacementId===saved.replacementId)){
  const box=$("weekNotice");
  box.hidden=false;
  box.classList.add("tt-room-alert");
  box.textContent=`Replacement was saved for ${dateShort(saved.replacementDate||p.date)}, but that date is not inside the currently generated Academic Weeks. Regenerate/check the semester Weeks to display it in the timetable.`;
 }
}

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
 if(action==="abandon"){
  if(!confirm(`Abandon ${term.name}?\n\nIts timetable and history will be kept, but its date range will no longer block creation of another semester.`))return;
  const r=await fetch(`/Timetable/AbandonTerm?termId=${encodeURIComponent(term.termId)}`,{method:"POST"});
  if(!r.ok){alert(await r.text());return;}
  await dashboard();
  return;
 }
 if(action==="undo-abandon"){
  const r=await fetch(`/Timetable/UndoAbandonTerm?termId=${encodeURIComponent(term.termId)}`,{method:"POST"});
  if(!r.ok){alert(await r.text());return;}
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

async function loadAudit(){
 const filter=$("auditFilter").value||"all",host=$("auditList"),summary=$("auditSummary");
 const r=await fetch(`/Timetable/Audit?status=${encodeURIComponent(filter)}`);const rows=r.ok?await r.json():[];
 const pending=rows.filter(x=>x.status==="Pending").length,resolved=rows.filter(x=>x.status==="Resolved").length;
 summary.innerHTML=`<span class="tt-audit-pending">Pending ${pending}</span><span class="tt-audit-resolved">Resolved ${resolved}</span>`;
 host.innerHTML=rows.length?"":"<div class='tt-empty'>No audit records.</div>";
 rows.forEach(a=>{const row=document.createElement("article");row.className=`tt-audit-item ${a.status.toLowerCase()}`;row.innerHTML=`<div><strong>${esc(a.issueType)}</strong><span>${dateShort(a.occurrenceDate)} · ${esc(a.timetableId)}</span></div><span class="tt-audit-state">${esc(a.status)}</span><p>${esc(a.reason||"")}</p>${a.beforeValue?`<small><b>Before:</b> ${esc(a.beforeValue)}</small>`:""}${a.afterValue?`<small><b>After:</b> ${esc(a.afterValue)}</small>`:""}${a.resolvedAt?`<small>Resolved ${dateShort(a.resolvedAt)}</small>`:""}`;host.appendChild(row)});
}

async function prepareHolidayHistory(){
 await loadHolidayList();
}
async function loadHolidayList(){
 const host=$("holidayList");
 const r=await fetch("/Timetable/Calendar");
 const rows=r.ok?await r.json():[];
 host.innerHTML=rows.length?"":"<div class='tt-empty'>No holidays saved in the central Holiday Calendar.</div>";
 rows.forEach(h=>{
  const row=document.createElement("div");row.className="tt-holiday-row";
  row.innerHTML=`<strong>${dateShort(h.eventDate)}</strong><span>${esc(h.name)}</span><span><small>${esc(h.eventType)}</small></span><button type="button">Delete</button>`;
  row.querySelector("button").onclick=()=>deleteHoliday(h.calendarEventId);
  host.appendChild(row);
 });
}
async function addHoliday(e){
 e.preventDefault();
 const payload={termId:null,eventDate:$("holidayDate").value,name:$("holidayName").value.trim(),eventType:$("holidayType").value,affectsClasses:true};
 const r=await fetch("/Timetable/AddCalendarEvent",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(payload)});
 if(!r.ok){showHolidayError(await r.text());return;}
 $("holidayError").hidden=true;$("holidayImportResult").hidden=true;$("holidayName").value="";$("holidayDate").value="";await loadHolidayList();if(S.term)await loadGrid();
}
async function loadMalaysiaHolidays(){
 const r=await fetch("/Timetable/LoadMalaysia2026Holidays",{method:"POST"});
 if(!r.ok){showHolidayError(await r.text());return;}
 const result=await r.json();$("holidayImportResult").textContent=`${result.added} Malaysia holiday(s) added; ${result.skipped} already existed.`;$("holidayImportResult").hidden=false;$("holidayError").hidden=true;await loadHolidayList();
}
async function importHolidayFile(file){
 if(!/\.(txt|csv)$/i.test(file.name)){showHolidayError("Only .txt or .csv files are accepted. CSV uses commas; TXT uses | separators.");return;}
 const fd=new FormData();fd.append("file",file);
 const r=await fetch("/Timetable/ImportCalendarEvents",{method:"POST",body:fd});
 if(!r.ok){showHolidayError(await r.text());$("holidayImportResult").hidden=true;$("holidayFile").value="";return;}
 const result=await r.json();
 $("holidayImportResult").textContent=`Imported ${result.added} holiday(s) successfully.`;$("holidayImportResult").hidden=false;$("holidayError").hidden=true;$("holidayFile").value="";await loadHolidayList();
}
function showHolidayError(message){$("holidayError").textContent=message;$("holidayError").hidden=false;}
async function deleteHoliday(id){if(!confirm("Delete this holiday from the central Holiday Calendar?"))return;const r=await fetch(`/Timetable/DeleteCalendarEvent?id=${encodeURIComponent(id)}`,{method:"POST"});if(!r.ok){showHolidayError(await r.text());return;}await loadHolidayList();if(S.term)await loadGrid();}

function occurrenceDate(weekStart,day){const d=new Date(weekStart+"T00:00:00"),delta=(day-d.getDay()+7)%7;d.setDate(d.getDate()+delta);return localIso(d);}
function isDateInsideSemester(date){
 if(!date||!S.term)return false;
 const d=String(date).slice(0,10),start=String(S.term.startDate).slice(0,10),end=String(S.term.endDate).slice(0,10);
 return d>=start&&d<=end;
}
function localIso(d){const y=d.getFullYear(),m=String(d.getMonth()+1).padStart(2,"0"),day=String(d.getDate()).padStart(2,"0");return `${y}-${m}-${day}`;}
function dateText(v){return new Date(v).toLocaleDateString();}
function dateShort(v){return new Date(v).toLocaleDateString(undefined,{day:"2-digit",month:"short",year:"numeric"});}
function dateDayShort(v){return new Date(v).toLocaleDateString(undefined,{day:"2-digit",month:"short"});}
function fmt(h){const p=h>=12?"PM":"AM",d=h>12?h-12:h;return `${d}:00 ${p}`;}
})();
