import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import Cookies from 'js-cookie'

// 公共布局
import PublicLayout from '@/layouts/PublicLayout.vue'
import AdminLayout from '@/layouts/AdminLayout.vue'

// 公共页面
import Login from '@/views/public/Login.vue'
import Register from '@/views/public/Register.vue'
import JobList from '@/views/public/JobList.vue'
import JobDetail from '@/views/public/JobDetail.vue'
import ResumeSubmit from '@/views/public/ResumeSubmit.vue'
import MyDeliveries from '@/views/public/MyDeliveries.vue'
import CandidateProfile from '@/views/public/CandidateProfile.vue'

// HR管理后台页面
import Dashboard from '@/views/admin/Dashboard.vue'
import JobManagement from '@/views/admin/JobManagement.vue'
import JobForm from '@/views/admin/JobForm.vue'
import SmartScreening from '@/views/admin/SmartScreening.vue'
import ResumeDetail from '@/views/admin/ResumeDetail.vue'
import CandidateComparison from '@/views/admin/CandidateComparison.vue'
import RecruitmentStrategy from '@/views/admin/RecruitmentStrategy.vue'
import AIInterviewManagement from '@/views/admin/AIInterviewManagement.vue'
import InterviewManagement from '@/views/admin/InterviewManagement.vue'
import Profile from '@/views/admin/Profile.vue'
import CommonInterviewerSetting from '@/views/admin/CommonInterviewerSetting.vue'

// 公共页面 - AI面试
import AIInterview from '@/views/public/AIInterview.vue'
import AIInterviewReport from '@/views/public/AIInterviewReport.vue'

// 路由配置
const routes: RouteRecordRaw[] = [
  // ========== 求职者端（招聘官网）==========
  {
    path: '/',
    component: PublicLayout,
    children: [
      { path: '', redirect: '/jobs' },
      { path: 'jobs', name: 'JobList', component: JobList, meta: { title: '岗位列表' } },
      { path: 'jobs/:id', name: 'JobDetail', component: JobDetail, meta: { title: '岗位详情' } },
      { path: 'resume/submit/:jobId', name: 'ResumeSubmit', component: ResumeSubmit, meta: { title: '投递简历', requiresAuth: true } },
      { path: 'my/deliveries', name: 'MyDeliveries', component: MyDeliveries, meta: { title: '我的投递', requiresAuth: true, role: 'candidate' } },
      { path: 'profile', name: 'CandidateProfile', component: CandidateProfile, meta: { title: '个人中心', requiresAuth: true, role: 'candidate' } },
      { path: 'ai-interview/:jobId/:deliveryId/:candidateId', name: 'AIInterview', component: AIInterview, meta: { title: 'AI面试', requiresAuth: true, role: 'candidate' } },
      { path: 'ai-interview/report/:sessionId', name: 'AIInterviewReport', component: AIInterviewReport, meta: { title: 'AI面试报告', requiresAuth: true, role: 'candidate' } },
    ]
  },

  // ========== 认证页面 ==========
  {
    path: '/login',
    name: 'Login',
    component: Login,
    meta: { title: '登录', public: true }
  },
  {
    path: '/register',
    name: 'Register',
    component: Register,
    meta: { title: '注册', public: true }
  },

  // ========== HR管理后台 ==========
  {
    path: '/admin',
    component: AdminLayout,
    children: [
      { path: '', redirect: '/admin/dashboard' },
      { path: 'dashboard', name: 'Dashboard', component: Dashboard, meta: { title: '工作台', requiresAuth: true, role: 'hr' } },
      { path: 'jobs', name: 'JobManagement', component: JobManagement, meta: { title: '岗位管理', requiresAuth: true, role: 'hr' } },
      { path: 'jobs/add', name: 'JobAdd', component: JobForm, meta: { title: '新增岗位', requiresAuth: true, role: 'hr' } },
      { path: 'jobs/edit/:id', name: 'JobEdit', component: JobForm, meta: { title: '编辑岗位', requiresAuth: true, role: 'hr' } },
      // ── 新三屏架构 ──
      { path: 'smart-screening', name: 'SmartScreening', component: SmartScreening, meta: { title: '智能筛选', requiresAuth: true, role: 'hr' } },
      { path: 'candidate-comparison', name: 'CandidateComparison', component: CandidateComparison, meta: { title: '人才对比', requiresAuth: true, role: 'hr' } },
      { path: 'recruitment-strategy', name: 'RecruitmentStrategy', component: RecruitmentStrategy, meta: { title: '招聘策略', requiresAuth: true, role: 'hr' } },
      // ── 旧路由保留组件（重定向至新路径）──
      { path: 'resumes', redirect: '/admin/smart-screening' },
      { path: 'resumes/:id', name: 'ResumeDetail', component: ResumeDetail, meta: { title: '简历详情', requiresAuth: true, role: 'hr' } },
      { path: 'ai-center', redirect: '/admin/smart-screening' },
      { path: 'statistics', redirect: '/admin/recruitment-strategy' },
      { path: 'knowledge-graph', redirect: '/admin/recruitment-strategy' },
      // ── 其他不变路由 ──
      { path: 'interviews', name: 'InterviewManagement', component: InterviewManagement, meta: { title: '面试管理', requiresAuth: true, role: 'hr' } },
      { path: 'ai-interviews', name: 'AIInterviewManagement', component: AIInterviewManagement, meta: { title: 'AI面试管理', requiresAuth: true, role: 'hr' } },
      { path: 'ai-interviews/:sessionId', name: 'AIInterviewDetail', component: AIInterviewManagement, meta: { title: 'AI面试详情', requiresAuth: true, role: 'hr' } },
      { path: 'profile', name: 'Profile', component: Profile, meta: { title: '个人中心', requiresAuth: true } },
      { path: 'interviewer-settings', name: 'CommonInterviewerSetting', component: CommonInterviewerSetting, meta: { title: '常用面试官设置', requiresAuth: true, role: 'hr' } },
    ]
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

// 路由守卫
router.beforeEach((to, _from, next) => {
  // 与请求拦截器保持一致：优先 Cookie，其次 localStorage
  const token = Cookies.get('token') || localStorage.getItem('token')
  const role = localStorage.getItem('role')

  // 公开页面直接放行
  if (to.meta.public) {
    next()
    return
  }

  // 需要登录的页面
  if (to.meta.requiresAuth) {
    if (!token) {
      next({ path: '/login', query: { redirect: to.fullPath } })
      return
    }

    // 角色权限检查
    if (to.meta.role) {
      if (role !== to.meta.role) {
        // 如果是HR访问求职者页面，跳转到管理后台
        if (to.meta.role === 'candidate' && role === 'hr') {
          next('/admin/dashboard')
          return
        }
        // 如果是求职者访问HR页面，跳转到首页
        if (to.meta.role === 'hr' && role === 'candidate') {
          next('/jobs')
          return
        }
      }
    }
  }

  next()
})

export default router