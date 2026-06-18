import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms'; 
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth';
import { debounceTime, Subject } from 'rxjs';

@Component({
  selector: 'app-failure-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './failure-list.html'
})
export class FailureListComponent implements OnInit {
  records: any[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 5;
  totalPages = 0;
  searchQuery = '';
  private searchSubject = new Subject<string>();

  constructor(private api: ApiService, public auth: AuthService) {}

  get isAdmin() {
    return this.auth.isAdmin();
  }

  ngOnInit(): void {
    // Задержка 300 мс перед отправкой поискового запроса
    this.searchSubject.pipe(debounceTime(300)).subscribe(() => {
      this.currentPage = 1;   // при новом поиске сбрасываем на первую страницу
      this.loadPage();
    });

    this.loadPage();
  }

  loadPage(): void {
    this.api.getFailureRecordsPage(this.currentPage, this.pageSize, this.searchQuery).subscribe({
      next: (data) => {
        this.records = data.items;
        this.totalCount = data.totalCount;
        this.totalPages = Math.ceil(this.totalCount / this.pageSize);
        // Если текущая страница оказалась больше максимальной, корректируем
        if (this.totalPages > 0 && this.currentPage > this.totalPages) {
          this.currentPage = this.totalPages;
          this.loadPage();
        }
      },
      error: (err) => console.error('Ошибка загрузки страницы', err)
    });
  }

  onSearch(): void {
    this.searchSubject.next(this.searchQuery);
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.onSearch();
  }

goToPage(page: number | string): void {
  const pageNum = typeof page === 'number' ? page : parseInt(page, 10);
  if (!isNaN(pageNum) && pageNum >= 1 && pageNum <= this.totalPages) {
    this.currentPage = pageNum;
    this.loadPage();
  }
}

  getPages(): (number | string)[] {
  const pages: (number | string)[] = [];
  const maxVisible = 5; // сколько номеров страниц отображаем рядом с текущей

  if (this.totalPages <= maxVisible + 2) {
    for (let i = 1; i <= this.totalPages; i++) pages.push(i);
    return pages;
  }

  pages.push(1);

  let start = Math.max(2, this.currentPage - Math.floor(maxVisible / 2));
  let end = Math.min(this.totalPages - 1, this.currentPage + Math.floor(maxVisible / 2));

  if (end - start + 1 < maxVisible) {
    if (start === 2) {
      end = Math.min(this.totalPages - 1, start + maxVisible - 1);
    } else if (end === this.totalPages - 1) {
      start = Math.max(2, end - maxVisible + 1);
    }
  }

  if (start > 2) pages.push('...');

  for (let i = start; i <= end; i++) pages.push(i);

  if (end < this.totalPages - 1) pages.push('...');

  pages.push(this.totalPages);
  return pages;
}

goToFirst(): void {
  this.goToPage(1);
}

goToLast(): void {
  this.goToPage(this.totalPages);
}

  delete(id: number): void {
    if (confirm('Удалить запись?')) {
      this.api.deleteFailureRecord(id).subscribe(() => this.loadPage());
    }
  }

  seedData(): void {
    if (confirm('Создать 100 тестовых отказов?')) {
      this.api.seedTestData().subscribe(() => this.loadPage());
    }
  }
showJumpModal = false;
jumpPage: number | null = null;

// Открыть модальное окно
openJumpModal(): void {
  this.jumpPage = null;
  this.showJumpModal = true;
}

// Переход на указанную страницу
confirmJump(): void {
  if (this.jumpPage && this.jumpPage >= 1 && this.jumpPage <= this.totalPages) {
    this.goToPage(this.jumpPage);
    this.closeJumpModal();
  }
}

// Закрыть модальное окно
closeJumpModal(): void {
  this.showJumpModal = false;
  this.jumpPage = null;
}

// Проверка, доступна ли кнопка "Перейти"
get canJump(): boolean {
  return this.jumpPage !== null && this.jumpPage >= 1 && this.jumpPage <= this.totalPages;
}
}