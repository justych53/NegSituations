import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-logs-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './logs-list.html'
})
export class LogsListComponent implements OnInit {
  logs: any[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 20;
  totalPages = 0;
  filterLevel = '';
  filterUser = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    this.api.getLogs(this.currentPage, this.pageSize, this.filterLevel, this.filterUser)
      .subscribe(data => {
        this.logs = data.items;
        this.totalCount = data.totalCount;
        this.totalPages = Math.ceil(this.totalCount / this.pageSize);
      });
  }

  onFilter(): void {
    this.currentPage = 1;
    this.loadLogs();
  }

  clearFilters(): void {
    this.filterLevel = '';
    this.filterUser = '';
    this.onFilter();
  }

goToPage(page: number | string): void {
  const pageNum = typeof page === 'number' ? page : parseInt(page, 10);
  if (!isNaN(pageNum) && pageNum >= 1 && pageNum <= this.totalPages) {
    this.currentPage = pageNum;
    this.loadLogs();
  }
}

  getPages(): (number | string)[] {
    const pages: (number | string)[] = [];
    const maxVisible = 5;
    if (this.totalPages <= maxVisible + 2) {
      for (let i = 1; i <= this.totalPages; i++) pages.push(i);
      return pages;
    }
    pages.push(1);
    let start = Math.max(2, this.currentPage - Math.floor(maxVisible / 2));
    let end = Math.min(this.totalPages - 1, this.currentPage + Math.floor(maxVisible / 2));
    if (start > 2) pages.push('...');
    for (let i = start; i <= end; i++) pages.push(i);
    if (end < this.totalPages - 1) pages.push('...');
    pages.push(this.totalPages);
    return pages;
  }

  getLevelBadge(level: string): string {
    switch (level) {
      case 'Error': return 'danger';
      case 'Warning': return 'warning';
      default: return 'info';
    }
  }
}