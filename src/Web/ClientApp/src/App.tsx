import { Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from './layout/AppLayout';
import { ErrorMonitorPage } from './pages/ErrorMonitor/ErrorMonitorPage';
import { ProviderManagementPage } from './pages/ProviderManagement/ProviderManagementPage';
import { CrawlReportPage } from './pages/CrawlReport/CrawlReportPage';
import { JobReportPage } from './pages/JobReport/JobReportPage';
import { NewsFeedPage } from './pages/NewsFeed/NewsFeedPage';

function App() {
  return (
    <AppLayout>
      <Routes>
        <Route path="/" element={<Navigate to="/feed" replace />} />
        <Route path="/feed" element={<NewsFeedPage />} />
        <Route path="/errors" element={<ErrorMonitorPage />} />
        <Route path="/providers" element={<ProviderManagementPage />} />
        <Route path="/reports" element={<CrawlReportPage />} />
        <Route path="/job-reports" element={<JobReportPage />} />
        <Route path="*" element={<Navigate to="/feed" replace />} />
      </Routes>
    </AppLayout>
  );
}

export default App;
