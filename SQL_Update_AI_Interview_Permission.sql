-- AI智能招聘管理系统 - 添加AI面试权限字段
-- 执行此脚本更新数据库结构

-- 为 Delivery 表添加 AI面试权限字段
ALTER TABLE Delivery ADD AllowAIInterview bit NOT NULL DEFAULT 0;
ALTER TABLE Delivery ADD AIInterviewDeadline datetime NULL;

-- 添加注释（如果数据库支持）
-- EXEC sp_addextendedproperty 'MS_Description', '是否允许进行AI面试', 'SCHEMA', 'dbo', 'TABLE', 'Delivery', 'COLUMN', 'AllowAIInterview';
-- EXEC sp_addextendedproperty 'MS_Description', 'AI面试截止时间', 'SCHEMA', 'dbo', 'TABLE', 'Delivery', 'COLUMN', 'AIInterviewDeadline';

PRINT '数据库更新成功：Delivery 表已添加 AllowAIInterview 和 AIInterviewDeadline 字段';
GO
