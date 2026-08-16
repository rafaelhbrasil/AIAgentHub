import { describe, it, expect } from 'vitest';
import { isPathForbiddenForBrowsing, isPathForbiddenForWorkspace } from './pathValidation';

describe('pathValidation', () => {
  const forbiddenList = [
    'C:\\Windows',
    'C:\\Program Files',
    '*:\\$Recycle.Bin',
    '*:\\Recovery',
    '/bin',
    '/etc',
    '/System',
  ];

  describe('isPathForbiddenForBrowsing', () => {
    it('should detect exact forbidden folders', () => {
      expect(isPathForbiddenForBrowsing('C:\\Windows', forbiddenList)).toBe(true);
      expect(isPathForbiddenForBrowsing('c:/windows', forbiddenList)).toBe(true);
      expect(isPathForbiddenForBrowsing('/bin', forbiddenList)).toBe(true);
    });

    it('should detect child folders of forbidden roots', () => {
      expect(isPathForbiddenForBrowsing('C:\\Windows\\System32', forbiddenList)).toBe(true);
      expect(isPathForbiddenForBrowsing('/etc/nginx/conf.d', forbiddenList)).toBe(true);
    });

    it('should detect wildcard matches on other drive letters', () => {
      expect(isPathForbiddenForBrowsing('D:\\$Recycle.Bin', forbiddenList)).toBe(true);
      expect(isPathForbiddenForBrowsing('D:\\$Recycle.Bin\\S-1-5-21', forbiddenList)).toBe(true);
    });

    it('should ALLOW browsing root drives', () => {
      expect(isPathForbiddenForBrowsing('C:\\', forbiddenList)).toBe(false);
      expect(isPathForbiddenForBrowsing('C:', forbiddenList)).toBe(false);
      expect(isPathForbiddenForBrowsing('D:\\', forbiddenList)).toBe(false);
      expect(isPathForbiddenForBrowsing('/', forbiddenList)).toBe(false);
    });

    it('should allow valid user project folders', () => {
      expect(isPathForbiddenForBrowsing('C:\\Projects\\MyApp', forbiddenList)).toBe(false);
      expect(isPathForbiddenForBrowsing('D:\\Code\\ai\\AgentHub', forbiddenList)).toBe(false);
      expect(isPathForbiddenForBrowsing('/home/user/code/project', forbiddenList)).toBe(false);
    });
  });

  describe('isPathForbiddenForWorkspace', () => {
    it('should BLOCK root drives from being workspace roots', () => {
      expect(isPathForbiddenForWorkspace('C:\\', forbiddenList)).toBe(true);
      expect(isPathForbiddenForWorkspace('C:', forbiddenList)).toBe(true);
      expect(isPathForbiddenForWorkspace('D:\\', forbiddenList)).toBe(true);
      expect(isPathForbiddenForWorkspace('/', forbiddenList)).toBe(true);
    });

    it('should BLOCK system folders from being workspace roots', () => {
      expect(isPathForbiddenForWorkspace('C:\\Windows', forbiddenList)).toBe(true);
      expect(isPathForbiddenForWorkspace('C:\\Windows\\System32', forbiddenList)).toBe(true);
      expect(isPathForbiddenForWorkspace('/bin', forbiddenList)).toBe(true);
    });

    it('should ALLOW valid user project folders as workspace roots', () => {
      expect(isPathForbiddenForWorkspace('C:\\Projects\\MyApp', forbiddenList)).toBe(false);
      expect(isPathForbiddenForWorkspace('D:\\Code\\ai\\AgentHub', forbiddenList)).toBe(false);
      expect(isPathForbiddenForWorkspace('/home/user/code/project', forbiddenList)).toBe(false);
    });
  });
});
