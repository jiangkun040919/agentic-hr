<template>
  <div class="jm-container">
    <el-tabs v-model="activeTab" class="jm-tabs">
      <!-- ═══ 岗位管理 ═══ -->
      <el-tab-pane label="岗位管理" name="jobs">
        <!-- 统计条 -->
        <div class="jm-stats">
          <div class="jms-item" :class="{ active: searchParams.status === undefined }" @click="searchParams.status=undefined;fetchJobs()"><span class="jms-num">{{ statsTotal }}</span><span class="jms-label">总岗位</span></div>
          <div class="jms-divider" />
          <div class="jms-item accent-green" :class="{ active: searchParams.status === 1 }" @click="searchParams.status = searchParams.status===1 ? undefined : 1; fetchJobs()"><span class="jms-num">{{ statsOpen }}</span><span class="jms-label">开放中</span></div>
          <div class="jms-divider" />
          <div class="jms-item accent-gray" :class="{ active: searchParams.status === 0 }" @click="searchParams.status = searchParams.status===0 ? undefined : 0; fetchJobs()"><span class="jms-num">{{ statsTotal - statsOpen }}</span><span class="jms-label">已关闭</span></div>
          <el-radio-group v-model="viewMode" size="small" style="margin-left:auto">
            <el-radio-button value="table"><el-icon><List /></el-icon> 表格</el-radio-button>
            <el-radio-button value="card"><el-icon><Grid /></el-icon> 卡片</el-radio-button>
          </el-radio-group>
        </div>

        <!-- 工具栏 -->
        <div class="jm-toolbar">
          <el-button type="primary" @click="$router.push('/admin/jobs/add')"><el-icon><Plus /></el-icon>发布新岗位</el-button>
          <el-input v-model="searchParams.keyword" placeholder="搜索岗位名称" clearable style="width:200px" @change="fetchJobs">
            <template #prefix><el-icon><Search /></el-icon></template>
          </el-input>
          <template v-if="selectedIds.length > 0">
            <div class="batch-bar">
              <span>已选 <b>{{ selectedIds.length }}</b> 项</span>
              <el-button size="small" type="success" @click="batchToggle(1)">批量上架</el-button>
              <el-button size="small" type="warning" @click="batchToggle(0)">批量下架</el-button>
              <el-button size="small" type="danger" @click="batchDelete">批量删除</el-button>
            </div>
          </template>
        </div>

        <!-- 表格视图 -->
        <el-card v-if="viewMode === 'table'" shadow="never" class="jm-card" v-loading="loading">
          <el-table :data="jobs" stripe @selection-change="onSelectionChange" class="jm-table">
            <el-table-column type="selection" width="40" />
            <el-table-column prop="jobId" label="ID" width="55" />
            <el-table-column prop="title" label="岗位名称" min-width="170" show-overflow-tooltip>
              <template #default="{ row }"><span class="tbl-title">{{ row.title }}</span></template>
            </el-table-column>
            <el-table-column prop="dept" label="部门" width="110">
              <template #default="{ row }"><el-tag :color="deptColor(row.dept)" effect="dark" size="small" round>{{ row.dept }}</el-tag></template>
            </el-table-column>
            <el-table-column prop="location" label="地点" width="90" />
            <el-table-column label="薪资" width="120">
              <template #default="{ row }">
                <span v-if="row.salaryMin&&row.salaryMax" class="tbl-salary">{{ formatSalary(row.salaryMin) }}-{{ formatSalary(row.salaryMax) }}</span>
                <span v-else class="tbl-salary-na">面议</span>
              </template>
            </el-table-column>
            <el-table-column prop="status" label="状态" width="85">
              <template #default="{ row }">
                <el-tag :type="row.status===1?'success':'info'" size="small" effect="light" round>{{ row.status===1?'开放':'关闭' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="createdAt" label="发布" width="110">
              <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="150" fixed="right">
              <template #default="{ row }">
                <el-button size="small" type="primary" link @click="handleEdit(row.jobId)">编辑</el-button>
                <el-button size="small" :type="row.status===1?'warning':'success'" link @click="toggleStatus(row)">{{ row.status===1?'下架':'上架' }}</el-button>
                <el-button size="small" type="danger" link @click="handleDelete(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>

        <!-- 卡片视图 -->
        <div v-else class="jm-cards" v-loading="loading">
          <el-card v-for="job in jobs" :key="job.jobId" class="jm-card-item" shadow="hover"
            :style="{ '--dept-color': deptColor(job.dept) }">
            <div class="card-accent" />
            <div class="card-top">
              <h3>{{ job.title }}</h3>
              <el-tag :type="job.status===1?'success':'info'" size="small" effect="light" round>{{ job.status===1?'开放':'关闭' }}</el-tag>
            </div>
            <div class="card-meta">
              <el-tag :color="deptColor(job.dept)" effect="dark" size="small" round>{{ job.dept }}</el-tag>
              <span class="meta-dot">·</span>
              <span><el-icon><Location /></el-icon>{{ job.location }}</span>
              <span class="meta-dot" v-if="job.salaryMin">·</span>
              <span v-if="job.salaryMin" class="meta-salary">{{ formatSalary(job.salaryMin) }}-{{ formatSalary(job.salaryMax) }}</span>
            </div>
            <div class="card-detail">
              <div class="cd-stat"><span class="cd-num">{{ job.deliveryCount || 0 }}</span>投递</div>
              <div class="cd-stat"><span class="cd-num">{{ job.interviewCount || 0 }}</span>面试</div>
              <span class="cd-date">{{ formatDate(job.createdAt) }}</span>
            </div>
            <div class="card-actions">
              <el-button size="small" @click="handleEdit(job.jobId)">编辑</el-button>
              <el-button size="small" :type="job.status===1?'warning':'success'" @click="toggleStatus(job)">{{ job.status===1?'下架':'上架' }}</el-button>
              <el-button size="small" type="danger" plain @click="handleDelete(job)">删除</el-button>
            </div>
          </el-card>
          <el-empty v-if="jobs.length===0" description="暂无岗位" />
        </div>

        <!-- 分页 -->
        <div class="jm-pagination" v-if="total>0">
          <el-pagination v-model:current-page="searchParams.page" v-model:page-size="searchParams.pageSize"
            :total="total" :page-sizes="[10,20,50]" layout="total, sizes, prev, pager, next" @change="fetchJobs" />
        </div>
      </el-tab-pane>

      <!-- ═══ 种子模板 ═══ -->
      <el-tab-pane label="种子模板" name="templates">
        <div style="margin-bottom:12px; display:flex; gap:8px">
          <el-button type="primary" size="small" @click="openCreateDialog">
            <el-icon><Plus /></el-icon>新增模板
          </el-button>
          <el-button type="success" size="small" :loading="llmGenerating" @click="openLlmGenerateDialog">
            <el-icon><MagicStick /></el-icon>LLM批量生成
          </el-button>
        </div>
        <el-card shadow="never">
          <el-table :data="templates" stripe v-loading="templateLoading" style="width: 100%">
            <el-table-column prop="templateId" label="ID" width="55" />
            <el-table-column prop="name" label="模板名称" min-width="150" />
            <el-table-column prop="category" label="类别" width="140" />
            <el-table-column label="技能标签" min-width="200">
              <template #default="{ row }">
                <span style="font-size:12px;color:var(--color-text-secondary)">
                  {{ (tryParseJson(row.hardSkillsRequired) || []).slice(0, 4).join(' · ') || '-' }}
                </span>
              </template>
            </el-table-column>
            <el-table-column label="采集进度" width="180">
              <template #default="{ row }">
                <el-progress
                  :percentage="Math.round(row.currentInstances / row.maxInstances * 100)"
                  :status="row.currentInstances >= row.maxInstances ? 'success' : ''"
                >
                  <span>{{ row.currentInstances }} / {{ row.maxInstances }}</span>
                </el-progress>
              </template>
            </el-table-column>
            <el-table-column prop="isActive" label="状态" width="80">
              <template #default="{ row }">
                <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '启用' : '停用' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="340" fixed="right">
              <template #default="{ row }">
                <el-button size="small" type="primary" link @click="openEditDialog(row)">编辑</el-button>
                <el-button size="small" type="success" link :loading="collectingId === row.templateId"
                  :disabled="row.currentInstances >= row.maxInstances" @click="handleCollect(row)">
                  {{ row.currentInstances >= row.maxInstances ? '已满' : '采集' }}
                </el-button>
                <el-button size="small" type="warning" link :loading="llmExtractingId === row.templateId" @click="handleLLMExtract(row)">
                  <el-icon><MagicStick /></el-icon>LLM提取
                </el-button>
                <el-button size="small" type="danger" link @click="handleTemplateDelete(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>

        <!-- ═══ LLM批量生成对话框 ═══ -->
        <el-dialog v-model="llmDialogVisible" title="LLM批量生成种子模版" width="500px" :close-on-click-modal="false">
          <p style="color:var(--color-text-secondary);margin-bottom:16px">输入部门名称，AI 自动生成该部门的 5-8 个典型岗位模版</p>
          <el-select v-model="llmDept" filterable allow-create default-first-option placeholder="选择或输入部门名" style="width:100%">
            <el-option v-for="d in deptOptions" :key="d" :label="d" :value="d" />
          </el-select>
          <template #footer>
            <el-button @click="llmDialogVisible = false">取消</el-button>
            <el-button type="primary" :loading="llmGenerating" @click="handleLlmGenerate" :disabled="!llmDept">
              开始生成
            </el-button>
          </template>
        </el-dialog>

        <!-- 模板弹窗（扩展版） -->
        <el-dialog v-model="dialogVisible" :title="dialogMode === 'create' ? '新增种子模板' : '编辑种子模板'" width="800px" :close-on-click-modal="false">
          <el-form :model="form" label-width="110px" :rules="rules" ref="formRef">
            <el-row :gutter="20">
              <el-col :span="12">
                <el-form-item label="模板名称" prop="name"><el-input v-model="form.name" placeholder="如：Java后端工程师" /></el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="类别" prop="category"><el-input v-model="form.category" placeholder="如：技术研发/后端" /></el-form-item>
              </el-col>
            </el-row>
            <el-form-item label="别名"><el-input v-model="form.aliases" placeholder='["Java开发","Java服务端"]' /></el-form-item>
            <el-form-item label="岗位职责"><el-input v-model="form.responsibilities" type="textarea" :rows="3" placeholder='["负责后端服务架构设计","参与系统性能优化"]' /></el-form-item>
            <el-row :gutter="20">
              <el-col :span="12">
                <el-form-item label="必备技能"><el-input v-model="form.hardSkillsRequired" placeholder='["Java","Spring Boot","MySQL"]' /></el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="加分技能"><el-input v-model="form.hardSkillsPreferred" placeholder='["Spring Cloud","K8s"]' /></el-form-item>
              </el-col>
            </el-row>
            <el-row :gutter="20">
              <el-col :span="12">
                <el-form-item label="软技能"><el-input v-model="form.softSkills" placeholder='["沟通协作","逻辑分析"]' /></el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="证书"><el-input v-model="form.certifications" placeholder='["PMP","AWS认证"]' /></el-form-item>
              </el-col>
            </el-row>
            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item label="学历要求"><el-input v-model="form.educationLevel" placeholder="本科及以上" /></el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="专业要求"><el-input v-model="form.educationMajor" placeholder="计算机相关" /></el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="每条上限"><el-input-number v-model="form.maxInstances" :min="1" :max="20" style="width:100%" /></el-form-item>
              </el-col>
            </el-row>
            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item label="初级年限"><el-input v-model="form.expJunior" placeholder="1-3年" /></el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="中级年限"><el-input v-model="form.expMid" placeholder="3-5年" /></el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="高级年限"><el-input v-model="form.expSenior" placeholder="5年+" /></el-form-item>
              </el-col>
            </el-row>
            <el-form-item label="搜索关键词"><el-input v-model="form.searchKeywords" type="textarea" :rows="2" placeholder='["Java 招聘 社招","Spring 工程师"]' /></el-form-item>
            <el-form-item label="排除词"><el-input v-model="form.excludeKeywords" placeholder='["实习","兼职","外包"]' /></el-form-item>
            <el-form-item label="采集平台"><el-input v-model="form.sourcePlatforms" placeholder='["BOSS直聘","拉勾网"]' /></el-form-item>
          </el-form>
          <template #footer>
            <el-button @click="dialogVisible = false">取消</el-button>
            <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
          </template>
        </el-dialog>
      </el-tab-pane>

      <!-- ═══ 新岗位发现 ═══ -->
      <el-tab-pane label="新岗位发现" name="discovered">
        <div class="discovered-badge" v-if="discoveredCount > 0" style="margin-bottom:12px">
          <el-tag type="warning" size="large">采集过程中自动发现的疑似新岗位 ({{ discoveredCount }})</el-tag>
        </div>
        <el-card shadow="never">
          <el-table :data="discoveredJobs" stripe v-loading="discoveredLoading" style="width: 100%">
            <el-table-column prop="title" label="岗位名称" min-width="200" />
            <el-table-column prop="sourcePlatform" label="来源" width="120" />
            <el-table-column label="相似度" width="100">
              <template #default="{ row }">
                <el-tag :type="row.similarityScore > 0.3 ? 'info' : 'danger'" size="small">
                  {{ row.similarityScore ? Math.round(row.similarityScore * 100) + '%' : 'N/A' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100">
              <template #default="{ row }">
                <el-tag :type="row.status === 'pending' ? 'warning' : row.status === 'approved' ? 'success' : 'danger'" size="small">
                  {{ row.status === 'pending' ? '待审核' : row.status === 'approved' ? '已确认' : '已驳回' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="createdAt" label="发现时间" width="170">
              <template #default="{ row }">{{ row.createdAt?.split('T')[0] }}</template>
            </el-table-column>
            <el-table-column label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <template v-if="row.status === 'pending'">
                  <el-button size="small" type="success" link @click="handleApprove(row)">确认并生成模板</el-button>
                  <el-button size="small" type="danger" link @click="handleReject(row)">驳回</el-button>
                </template>
                <el-tag v-else-if="row.status === 'approved'" type="success" size="small">已生成模板</el-tag>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="discoveredJobs.length === 0 && !discoveredLoading" description="暂无新岗位发现" :image-size="80" />
        </el-card>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useJobStore } from '@/stores/job'
import { getJobList } from '@/api/job'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Search, Location, Grid, List, MagicStick } from '@element-plus/icons-vue'
import dayjs from 'dayjs'
import { formatSalary } from '@/utils/format'
import { request } from '@/utils/request'
import type { FormInstance } from 'element-plus'

const router = useRouter()
const jobStore = useJobStore()
const activeTab = ref('jobs')

// ═══ 岗位管理 ═══
const loading = computed(() => jobStore.loading)
const jobs = computed(() => jobStore.jobs)
const total = computed(() => jobStore.total)
const viewMode = ref('table')
const selectedIds = ref<number[]>([])
const searchParams = reactive({ page: 1, pageSize: 10, keyword: '', status: undefined as any })
const statsTotal = ref(0)
const statsOpen = ref(0)

const fetchStats = async () => {
  try {
    const [allRes, openRes] = await Promise.all([
      getJobList({ page: 1, pageSize: 1 }),
      getJobList({ page: 1, pageSize: 1, status: 1 }),
    ])
    const allData = (allRes as any)?.data || allRes
    const openData = (openRes as any)?.data || openRes
    statsTotal.value = allData?.total || 0
    statsOpen.value = openData?.total || 0
  } catch {}
}

const deptColor = (d: string) => ({
  '技术部':'#409EFF','AI部':'#9B59B6','数据部':'#67C23A','产品部':'#E67C1A',
  '前端部':'#00D2FF','运营部':'#F56C6C','运维部':'#FF9800','测试部':'#00BCD4',
  '财务部':'#2C3E50','人力资源部':'#FF85C0','安全部':'#E91E63','设计部':'#FF5722',
  '架构部':'#795548','市场部':'#36CFC9'
}[d] || '#409EFF')

const fetchJobs = () => jobStore.fetchJobs(searchParams)
const formatDate = (d: string) => dayjs(d).format('YYYY-MM-DD')
const handleEdit = (id: number) => router.push(`/admin/jobs/edit/${id}`)
const onSelectionChange = (rows: any[]) => { selectedIds.value = rows.map(r => r.jobId) }
const toggleStatus = async (row: any) => {
  const ns = row.status===1?0:1
  try { await jobStore.toggleStatus(row.jobId,ns); ElMessage.success(ns===1?'已上架':'已下架'); fetchJobs() }
  catch(e:any) { ElMessage.error(e.message||'失败') }
}
const handleDelete = (row: any) => {
  ElMessageBox.confirm('确定删除？','提示',{type:'warning'}).then(async()=>{
    try { await jobStore.remove(row.jobId); ElMessage.success('已删除'); fetchJobs() }
    catch(e:any) { ElMessage.error(e.message||'失败') }
  })
}
const batchToggle = async (s: number) => {
  ElMessageBox.confirm(`确定批量${s===1?'上架':'下架'}？`,'批量操作',{type:'warning'}).then(async()=>{
    try { await Promise.all(selectedIds.value.map(id=>jobStore.toggleStatus(id,s))); ElMessage.success('完成'); fetchJobs(); selectedIds.value=[] }
    catch(e:any) { ElMessage.error(e.message||'失败') }
  })
}
const batchDelete = async () => {
  ElMessageBox.confirm(`确定删除 ${selectedIds.value.length} 个岗位？`,'批量删除',{type:'warning'}).then(async()=>{
    try { await Promise.all(selectedIds.value.map(id=>jobStore.remove(id))); ElMessage.success('完成'); fetchJobs(); selectedIds.value=[] }
    catch(e:any) { ElMessage.error(e.message||'失败') }
  })
}

// ═══ 种子模板 ═══
const templateLoading = ref(false)
const saving = ref(false)
const llmGenerating = ref(false)
const llmDialogVisible = ref(false)
const llmDept = ref('')
const deptOptions = ['技术部', 'AI部', '前端部', '数据部', '产品部', '架构部', '运维部', '测试部', '安全部', '设计部', '运营部', '市场部', '财务部', '人力资源部']
const templates = ref<any[]>([])
const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const editId = ref(0)
const collectingId = ref(0)
const llmExtractingId = ref(0)
const formRef = ref<FormInstance>()
const form = reactive({
  name: '', category: '', aliases: '[]', responsibilities: '[]',
  hardSkillsRequired: '[]', hardSkillsPreferred: '[]', softSkills: '[]',
  educationLevel: '', educationMajor: '',
  expJunior: '', expMid: '', expSenior: '',
  certifications: '[]', searchKeywords: '[]',
  excludeKeywords: '[]', sourcePlatforms: '[]', maxInstances: 5
})
const rules = {
  name: [{ required: true, message: '请输入模板名称', trigger: 'blur' }],
  category: [{ required: true, message: '请输入类别', trigger: 'blur' }]
}

const loadTemplates = async () => {
  templateLoading.value = true
  try { const res = await request.get('/seed-templates', { params: { pageSize: 50 } }); templates.value = res.items || [] }
  catch { ElMessage.error('加载失败') }
  finally { templateLoading.value = false }
}

const openCreateDialog = () => {
  dialogMode.value = 'create'; editId.value = 0
  form.name = ''; form.category = ''; form.aliases = '[]'; form.responsibilities = '[]'
  form.hardSkillsRequired = '[]'; form.hardSkillsPreferred = '[]'; form.softSkills = '[]'
  form.educationLevel = ''; form.educationMajor = ''
  form.expJunior = ''; form.expMid = ''; form.expSenior = ''
  form.certifications = '[]'; form.searchKeywords = '[]'
  form.excludeKeywords = '[]'; form.sourcePlatforms = '[]'; form.maxInstances = 5
  dialogVisible.value = true
}

const openEditDialog = (row: any) => {
  dialogMode.value = 'edit'; editId.value = row.templateId
  request.get(`/seed-templates/${row.templateId}`).then((data: any) => {
    form.name = data.name || ''; form.category = data.category || ''
    form.aliases = data.aliases || '[]'; form.responsibilities = data.responsibilities || '[]'
    form.hardSkillsRequired = data.hardSkillsRequired || '[]'; form.hardSkillsPreferred = data.hardSkillsPreferred || '[]'
    form.softSkills = data.softSkills || '[]'; form.educationLevel = data.educationLevel || ''
    form.educationMajor = data.educationMajor || ''; form.expJunior = data.expJunior || ''
    form.expMid = data.expMid || ''; form.expSenior = data.expSenior || ''
    form.certifications = data.certifications || '[]'; form.searchKeywords = data.searchKeywords || '[]'
    form.excludeKeywords = data.excludeKeywords || '[]'; form.sourcePlatforms = data.sourcePlatforms || '[]'
    form.maxInstances = data.maxInstances || 5; dialogVisible.value = true
  })
}

const handleSave = async () => {
  if (!formRef.value) return
  try { await formRef.value.validate() } catch { return }
  saving.value = true
  try {
    if (dialogMode.value === 'create') { await request.post('/seed-templates', { ...form }); ElMessage.success('创建成功') }
    else { await request.put(`/seed-templates/${editId.value}`, { ...form }); ElMessage.success('更新成功') }
    dialogVisible.value = false; await loadTemplates()
  } catch (e: any) { ElMessage.error(e.response?.data?.message || '操作失败') }
  finally { saving.value = false }
}

const handleCollect = async (row: any) => {
  collectingId.value = row.templateId
  try {
    const res = await request.post(`/seed-templates/${row.templateId}/collect`)
    ElMessage.success(`采集完成：新增 ${res.collected || 0} 条岗位，发现 ${res.discovered || 0} 个新岗位`)
    await loadTemplates(); await loadDiscoveredJobs()
  } catch (e: any) { ElMessage.error(e.response?.data?.message || '采集失败') }
  finally { collectingId.value = 0 }
}

const handleLLMExtract = async (row: any) => {
  llmExtractingId.value = row.templateId
  try { const res = await request.post(`/seed-templates/${row.templateId}/llm-extract`); ElMessage.success(`LLM提取完成：新增 ${res.collected || 0} 条岗位`); await loadTemplates() }
  catch (e: any) { ElMessage.error(e.response?.data?.message || 'LLM提取失败') }
  finally { llmExtractingId.value = 0 }
}

// ═══ LLM批量生成 ═══
const openLlmGenerateDialog = () => { llmDept.value = ''; llmDialogVisible.value = true }

const handleLlmGenerate = async () => {
  if (!llmDept.value) return
  llmGenerating.value = true
  try {
    const res = await request.post('/seed-templates/llm-generate', { department: llmDept.value })
    ElMessage.success(res.message || `成功生成 ${res.count || 0} 个模版`)
    llmDialogVisible.value = false
    await loadTemplates()
  } catch (e: any) {
    ElMessage.error(e.response?.data?.message || 'LLM生成失败，请重试')
  } finally {
    llmGenerating.value = false
  }
}

const tryParseJson = (str: string | undefined | null) => {
  if (!str) return []
  try { return JSON.parse(str) } catch { return [] }
}

const handleTemplateDelete = async (row: any) => {
  try {
    await ElMessageBox.confirm(`确定删除模板"${row.name}"？`, '确认删除', { type: 'warning' })
    await request.delete(`/seed-templates/${row.templateId}`); ElMessage.success('删除成功'); await loadTemplates()
  } catch {}
}

// ═══ 新岗位发现 ═══
const discoveredLoading = ref(false)
const discoveredJobs = ref<any[]>([])
const discoveredCount = ref(0)

const loadDiscoveredJobs = async () => {
  discoveredLoading.value = true
  try { const res = await request.get('/discovered-jobs', { params: { pageSize: 50 } }); discoveredJobs.value = res.items || []; discoveredCount.value = res.items?.length || 0 }
  catch { ElMessage.error('加载失败') }
  finally { discoveredLoading.value = false }
}

const handleApprove = async (row: any) => {
  try { const res = await request.post(`/discovered-jobs/${row.id}/approve`); ElMessage.success(res.message || '已确认，新模板已自动生成'); await loadDiscoveredJobs(); await loadTemplates() }
  catch (e: any) { ElMessage.error(e.response?.data?.message || '操作失败') }
}

const handleReject = async (row: any) => {
  try { await request.post(`/discovered-jobs/${row.id}/reject`); ElMessage.success('已驳回'); await loadDiscoveredJobs() }
  catch { ElMessage.error('操作失败') }
}

onMounted(() => { fetchJobs(); fetchStats(); loadTemplates(); loadDiscoveredJobs() })
</script>

<style scoped lang="scss">
.jm-container { max-width: var(--content-max-width); }

.jm-tabs {
  :deep(.el-tabs__header) { margin-bottom: 12px; }
}

// ====== 统计条 ======
.jm-stats {
  display: flex; gap: 0; align-items: center; padding: var(--space-4) var(--space-6);
  background: var(--color-surface); border: 1px solid var(--color-border); border-radius: var(--radius-lg);
  margin-bottom: var(--space-4); box-shadow: var(--shadow-card);
  .jms-item { text-align: center; padding: var(--space-2) var(--space-6); border-radius: var(--radius-md); cursor: pointer; transition: all var(--duration-fast) var(--ease-out);
    .jms-num { display: block; font-size: var(--text-2xl); font-weight: var(--weight-bold); color: var(--color-text); font-family: var(--font-mono); }
    .jms-label { font-size: var(--text-xs); color: var(--color-text-secondary); margin-top: 2px; }
    &:hover { background: var(--color-surface-hover); }
    &.active { background: var(--color-primary-bg); .jms-num { color: var(--color-primary); } .jms-label { color: var(--color-primary); } }
    &.accent-green { .jms-num { color: var(--color-success); } &.active { background: var(--color-success-bg); .jms-label { color: var(--color-success); } } }
    &.accent-gray { .jms-num { color: var(--color-text-muted); } &.active { background: var(--color-bg); .jms-label { color: var(--color-text-secondary); } } }
  }
  .jms-divider { width: 1px; height: 32px; background: var(--color-border); }
}

// ====== 工具栏 ======
.jm-toolbar {
  display: flex; gap: var(--space-3); align-items: center; margin-bottom: var(--space-4); flex-wrap: wrap;
  .batch-bar {
    display: flex; align-items: center; gap: var(--space-2); padding: var(--space-1) var(--space-3);
    background: var(--color-primary-bg); border: 1px solid var(--color-border-glow); border-radius: var(--radius-md);
    font-size: var(--text-sm); color: var(--color-primary);
    b { color: var(--color-primary); }
  }
}

// ====== 表格 ======
.jm-card { border-radius: var(--radius-lg); border: 1px solid var(--color-border); :deep(.el-card__body) { padding: 0; } }
.jm-table {
  .tbl-title { font-weight: var(--weight-medium); color: var(--color-primary); }
  .tbl-salary { font-weight: var(--weight-semibold); color: var(--color-accent); }
  .tbl-salary-na { color: var(--color-text-muted); }
}

// ====== 卡片网格 ======
.jm-cards {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: var(--space-4);
  min-height: 300px;
}
.jm-card-item {
  cursor: default; border-radius: var(--radius-xl); overflow: hidden; border: 1px solid var(--color-border);
  transition: all var(--duration-fast) var(--ease-out); position: relative;
  .card-accent { position: absolute; top: 0; left: 0; right: 0; height: 3px; background: var(--dept-color, var(--color-primary)); }
  &:hover { transform: translateY(-2px); box-shadow: var(--shadow-glow); border-color: var(--color-border-glow); }
  .card-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-3);
    h3 { font-size: var(--text-md); color: var(--color-text); margin: 0; font-weight: var(--weight-semibold); }
  }
  .card-meta { display: flex; align-items: center; gap: var(--space-2); margin-bottom: var(--space-3); font-size: var(--text-sm); color: var(--color-text-secondary);
    .meta-dot { color: var(--color-text-muted); }
    .meta-salary { color: var(--color-accent); font-weight: var(--weight-semibold); }
  }
  .card-detail { display: flex; align-items: center; gap: var(--space-4); margin-bottom: var(--space-4); padding-bottom: var(--space-4); border-bottom: 1px solid var(--color-border-light);
    .cd-stat { font-size: var(--text-xs); color: var(--color-text-secondary);
      .cd-num { font-weight: var(--weight-semibold); color: var(--color-text); margin-right: 2px; }
    }
    .cd-date { margin-left: auto; font-size: var(--text-xs); color: var(--color-text-muted); }
  }
  .card-actions { display: flex; justify-content: flex-end; gap: var(--space-2); }
}

.jm-pagination { margin-top: var(--space-5); display: flex; justify-content: center; }
</style>
