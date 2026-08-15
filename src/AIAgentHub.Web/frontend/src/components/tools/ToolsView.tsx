import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { McpDto, SkillDto } from '../../types/settings';

export const ToolsView: React.FC = () => {
  const [mcps, setMcps] = useState<McpDto[]>([]);
  const [skills, setSkills] = useState<SkillDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    const fetchTools = async () => {
      setIsLoading(true);
      try {
        const [mcpRes, skillRes] = await Promise.all([
          apiFetch<McpDto[]>('/api/v1/mcps'),
          apiFetch<SkillDto[]>('/api/v1/skills'),
        ]);

        if (mcpRes.ok && mcpRes.data) setMcps(mcpRes.data);
        if (skillRes.ok && skillRes.data) setSkills(skillRes.data);
      } finally {
        setIsLoading(false);
      }
    };
    fetchTools();
  }, []);

  return (
    <div>
      <h2>Model Context Protocol (MCP) & Provider Skills</h2>
      <p className="card-subtitle">
        Expose reusable tools and specialized workflows to AI coding assistants.
      </p>

      <div style={{ marginTop: '20px' }} className="grid-cols-3">
        <div className="card glass">
          <div className="card-title">Registered MCP Servers</div>
          <div className="stat-val">{isLoading ? '...' : mcps.length}</div>
          <p className="card-subtitle" style={{ marginTop: '10px' }}>
            Connect external dev tools and resource providers.
          </p>
        </div>

        <div className="card glass">
          <div className="card-title">Installed Skills</div>
          <div className="stat-val">{isLoading ? '...' : skills.length}</div>
          <p className="card-subtitle" style={{ marginTop: '10px' }}>
            Provider-agnostic domain workflows and agents.
          </p>
        </div>
      </div>
    </div>
  );
};
