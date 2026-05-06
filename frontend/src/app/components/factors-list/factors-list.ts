import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-factors-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './factors-list.html'
})
export class FactorsListComponent implements OnInit {
  factors: any[] = [];
  newFactorName = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadFactors();
  }

  loadFactors(): void {
    this.api.getFactors().subscribe(data => this.factors = data);
  }

  add(): void {
    if (!this.newFactorName.trim()) return;
    this.api.createFactor(this.newFactorName).subscribe(() => {
      this.newFactorName = '';
      this.loadFactors();
    });
  }

  delete(id: number): void {
    if (confirm('Удалить фактор?')) {
      this.api.deleteFactor(id).subscribe(() => this.loadFactors());
    }
  }
}