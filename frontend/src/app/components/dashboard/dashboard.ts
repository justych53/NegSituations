import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { StatisticsService } from '../../services/statistics.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, BaseChartDirective],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.css']
})
export class DashboardComponent implements OnInit {
  dataLoaded = false;         
  totalFailures = 0;
  avgParticipants = 0;
  topParticipants: any[] = [];
  factorAverages: any[] = [];

  barChartData: ChartData<'bar'> = { labels: [], datasets: [{ data: [], label: 'Средняя вина (%)' }] };
  barChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: { y: { beginAtZero: true, max: 100 } }
  };

  pieChartData: ChartData<'pie'> = {
    labels: [],
    datasets: [{
      data: [],
      backgroundColor: ['#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', '#FF9F40']
    }]
  };
  pieChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' } }
  };

  constructor(private statsService: StatisticsService) {}

  ngOnInit(): void {
    this.statsService.getDashboardData().subscribe(data => {
      this.totalFailures = data.totalFailures;
      this.avgParticipants = data.avgParticipants;
      this.topParticipants = data.topParticipants;
      this.factorAverages = data.factorAverages;

      // Данные для графиков
      this.barChartData.labels = this.topParticipants.map(p => p.name);
      this.barChartData.datasets[0].data = this.topParticipants.map(p => p.avgWeight * 100);

      this.pieChartData.labels = this.factorAverages.map(f => f.name);
      this.pieChartData.datasets[0].data = this.factorAverages.map(f => f.avgWeight * 100);

      // Показываем графики только после получения данных
      this.dataLoaded = true;
    });
  }
}