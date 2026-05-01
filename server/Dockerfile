FROM php:8.5-apache@sha256:e55e196fca35436fd26ba36e6fd2f2d9846142d0561dc35f74c7d25db1d54318
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80