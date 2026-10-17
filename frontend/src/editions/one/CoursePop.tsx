import type { Course } from '../../api/types'
import { useStore } from '../../lib/store-context'
import type { CourseBlock } from '../../lib/course-blocks'
import { CardPopover } from './CardPopover'

type DisplayCourse = Course | CourseBlock

export function CoursePop() {
  const { sheet, openSheet } = useStore()
  const course = sheet?.type === 'course' ? sheet.course : null
  return <CardPopover open={!!course} identity={course?.id || ''} label={course?.name || ''}
    source="button.sw-cell, button.one-card"
    detailsSelector="dt, dd:not([data-course-text]):not(.one-pop-times), .one-pop-times > span:not([data-pop-shared]), button" onClose={() => openSheet(null)}>
    {course && <CourseBody course={course} onClose={() => openSheet(null)} />}
  </CardPopover>
}

function CourseBody({ course, onClose }: { course: DisplayCourse; onClose(): void }) {
  const parts = 'parts' in course ? course.parts : [course]
  const times = [...new Set(parts.filter((part) => part.start && part.end).map((part) => `${part.start}–${part.end}`))]
  return (
    <>
      <h2 data-course-text="title">{course.name}</h2>
      <dl className="one-pop-facts">
        {course.room && <div><dt>地点</dt><dd className="lat" data-course-text="room">{course.room}</dd></div>}
        {times.length > 0 && <div><dt>时间</dt><dd className="one-pop-times num lat">{times.map((time) => <span key={time} data-course-text="time">{time}</span>)}</dd></div>}
        {course.teacher && <div><dt>教师</dt><dd>{course.teacher}</dd></div>}
      </dl>
      <button className="one-pop-c-x" onClick={onClose} aria-label="关闭">✕</button>
    </>
  )
}
